﻿using System;
using System.Collections.Generic;
using System.Linq;

using BizHawk.Common;
using BizHawk.Common.StringExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.Common
{
	/// <summary>
	/// will hold buttons for 1 frame and then release them. (Calling Click() from your button click is what you want to do)
	/// TODO - should the duration be controllable?
	/// </summary>
	public class ClickyVirtualPadController : IController
	{
		public ControllerDefinition Type { get; set; }

		public bool this[string button]
		{
			get { return IsPressed(button); }
		}

		public float GetFloat(string name)
		{
			return 0.0f;
		}

		// TODO
		public bool IsPressed(string button)
		{
			return _pressed.Contains(button);
		}

		/// <summary>
		/// call this once per frame to do the timekeeping for the hold and release
		/// </summary>
		public void FrameTick()
		{
			_pressed.Clear();
		}

		/// <summary>
		/// call this to hold the button down for one frame
		/// </summary>
		public void Click(string button)
		{
			_pressed.Add(button);
		}

		public void Unclick(string button)
		{
			_pressed.Remove(button);
		}

		public void Toggle(string button)
		{
			if (IsPressed(button))
			{
				_pressed.Remove(button);
			}
			else
			{
				_pressed.Add(button);
			}
		}

		public void SetBool(string button, bool value)
		{
			if (value)
			{
				_pressed.Remove(button);
			}
			else
			{
				_pressed.Add(button);
			}
		}

		private readonly HashSet<string> _pressed = new HashSet<string>();
	}

	/// <summary>
	/// Filters input for things called Up and Down while considering the client's AllowUD_LR option. 
	/// This is a bit gross but it is unclear how to do it more nicely
	/// </summary>
	public class UD_LR_ControllerAdapter : IController
	{
		public ControllerDefinition Type
		{
			get { return Source.Type; }
		}

		public bool this[string button]
		{
			get { return IsPressed(button); }
		}

		public IController Source { get; set; }

		// The float format implies no U+D and no L+R no matter what, so just passthru
		public float GetFloat(string name)
		{
			return Source.GetFloat(name);
		}

		HashSet<string> Unpresses = new HashSet<string>();

		public bool IsPressed(string button)
		{
			bool PriorityUD_LR = !Global.Config.AllowUD_LR && !Global.Config.ForbidUD_LR; //implied by neither of the others being set (left as non-enum for back-compatibility)


			if (Global.Config.AllowUD_LR)
			{
				return Source.IsPressed(button);
			}

			string prefix;

			//" C " is for N64 "P1 C Up" and the like, which should not be subject to mutexing

			//regarding the unpressing and UDLR logic...... don't think about it. don't question it. don't look at it.

			if (button.Contains("Down") && !button.Contains(" C "))
			{
				if (!Source.IsPressed(button)) Unpresses.Remove(button);
				prefix = button.GetPrecedingString("Down");
				string other = prefix + "Up";
				if (Source.IsPressed(other))
				{
					if (Unpresses.Contains(button)) return false;
					if (Global.Config.ForbidUD_LR) return false;
					Unpresses.Add(other);
				}
				else Unpresses.Remove(button);
			}

			if (button.Contains("Up") && !button.Contains(" C "))
			{
				if (!Source.IsPressed(button)) Unpresses.Remove(button);
				prefix = button.GetPrecedingString("Up");
				string other = prefix + "Down";
				if (Source.IsPressed(other))
				{
					if (Unpresses.Contains(button)) return false;
					if (Global.Config.ForbidUD_LR) return false;
					Unpresses.Add(other);
				}
				else Unpresses.Remove(button);
			}


			if (button.Contains("Right") && !button.Contains(" C "))
			{
				if (!Source.IsPressed(button)) Unpresses.Remove(button);
				prefix = button.GetPrecedingString("Right");
				string other = prefix + "Left";
				if (Source.IsPressed(other))
				{
					if (Unpresses.Contains(button)) return false;
					if (Global.Config.ForbidUD_LR) return false;
					Unpresses.Add(other);
				}
				else Unpresses.Remove(button);
			}

			if (button.Contains("Left") && !button.Contains(" C "))
			{
				if (!Source.IsPressed(button)) Unpresses.Remove(button);
				prefix = button.GetPrecedingString("Left");
				string other = prefix + "Right";
				if (Source.IsPressed(other))
				{
					if (Unpresses.Contains(button)) return false;
					if (Global.Config.ForbidUD_LR) return false;
					Unpresses.Add(other);
				}
				else Unpresses.Remove(button);
			}

			return Source.IsPressed(button);
		}
	}

	public class SimpleController : IController
	{
		public ControllerDefinition Type { get; set; }

		protected WorkingDictionary<string, bool> Buttons = new WorkingDictionary<string, bool>();
		protected WorkingDictionary<string, float> Floats = new WorkingDictionary<string, float>();

		public virtual void Clear()
		{
			Buttons = new WorkingDictionary<string, bool>();
			Floats = new WorkingDictionary<string, float>();
		}

		public virtual bool this[string button]
		{
			get { return Buttons[button]; }
			set { Buttons[button] = value; }
		}

		public virtual bool IsPressed(string button)
		{
			return this[button];
		}

		public float GetFloat(string name)
		{
			return Floats[name];
		}

		public IEnumerable<KeyValuePair<string, bool>> BoolButtons()
		{
			return Buttons;
		}

		public virtual void LatchFrom(IController source)
		{
			foreach (var button in source.Type.BoolButtons)
			{
				Buttons[button] = source[button];
			}
		}

		public void AcceptNewFloats(IEnumerable<Tuple<string, float>> newValues)
		{
			foreach (var sv in newValues)
			{
				Floats[sv.Item1] = sv.Item2;
			}
		}
	}

	// Used by input display, to determine if either autofire or regular stickies are "in effect" because we color this scenario differently
	public class StickyOrAdapter : IController
	{
		public bool IsPressed(string button)
		{
			return this[button];
		}

		// pass floats solely from the original source
		// this works in the code because SourceOr is the autofire controller
		public float GetFloat(string name)
		{
			int i = Source.Type.FloatControls.IndexOf(name);
			return Source.Type.FloatRanges[i].Mid; // Floats don't make sense in sticky land
		}

		public ISticky Source { get; set; }
		public ISticky SourceStickyOr { get; set; }
		public ControllerDefinition Type { get { return Source.Type; } set { throw new InvalidOperationException(); } }

		public bool this[string button]
		{
			get
			{
				return Source.StickyIsInEffect(button) ||
					SourceStickyOr.StickyIsInEffect(button);
			}

			set
			{
				throw new InvalidOperationException();
			}
		}
	}

	public interface ISticky : IController
	{
		bool StickyIsInEffect(string button);
	}

	public class StickyXorAdapter : IController, ISticky
	{
		protected HashSet<string> stickySet = new HashSet<string>();

		public IController Source { get; set; }

		public ControllerDefinition Type
		{
			get { return Source.Type; }
			set { throw new InvalidOperationException(); }
		}

		public bool Locked { get; set; } // Pretty much a hack, 

		public bool IsPressed(string button)
		{
			return this[button];
		}

		// if SetFloat() is called (typically virtual pads), then that float will entirely override the Source input
		// otherwise, the source is passed thru.
		protected readonly WorkingDictionary<string, float?> _floatSet = new WorkingDictionary<string, float?>();

		public void SetFloat(string name, float? value)
		{
			if (value.HasValue)
			{
				_floatSet[name] = value;
			}
			else
			{
				_floatSet.Remove(name);
			}
		}

		public float GetFloat(string name)
		{
			var val = _floatSet[name];

			if (val.HasValue)
			{
				return val.Value;
			}

			if (Source == null)
			{
				return 0;
			}

			return Source.GetFloat(name);
		}

		public void ClearStickyFloats()
		{
			_floatSet.Clear();
		}

		public bool this[string button]
		{
			get
			{
				var source = Source[button];
				source ^= stickySet.Contains(button);
				return source;
			}

			set
			{
				throw new InvalidOperationException();
			}
		}

		/// <summary>
		/// Determines if a sticky is current mashing the button itself,
		/// If sticky is not set then false, if set, it returns true if the Source is not pressed, else false
		/// </summary>
		public bool StickyIsInEffect(string button)
		{
			if (IsSticky(button))
			{
				return !Source.IsPressed(button);
			}

			return false;
		}

		public void SetSticky(string button, bool isSticky)
		{
			if (isSticky)
			{
				stickySet.Add(button);
			}
			else
			{
				stickySet.Remove(button);
			}
		}

		public void Unset(string button)
		{
			stickySet.Remove(button);
			_floatSet.Remove(button);
		}

		public bool IsSticky(string button)
		{
			return stickySet.Contains(button);
		}

		public HashSet<string> CurrentStickies
		{
			get
			{
				return stickySet;
			}
		}

		public void ClearStickies()
		{
			stickySet.Clear();
			_floatSet.Clear();
		}

		public void MassToggleStickyState(List<string> buttons)
		{
			foreach (var button in buttons.Where(button => !_justPressed.Contains(button)))
			{
				if (stickySet.Contains(button))
				{
					stickySet.Remove(button);
				}
				else
				{
					stickySet.Add(button);
				}
			}

			_justPressed = buttons;
		}

		private List<string> _justPressed = new List<string>();
	}

	///// SuuperW: I'm leaving the old class in case I accidentally screwed something up
	//public class AutoFireStickyXorAdapter : IController, ISticky
	//{
	//	public int On { get; set; }
	//	public int Off { get; set; }
	//	public WorkingDictionary<string, int> buttonStarts = new WorkingDictionary<string, int>();
	//	public WorkingDictionary<string, int> lagStarts = new WorkingDictionary<string, int>(); // TODO: need a data structure not misc dictionaries

	//	private readonly HashSet<string> _stickySet = new HashSet<string>();

	//	public IController Source { get; set; }

	//	public void SetOnOffPatternFromConfig()
	//	{
	//		On = Global.Config.AutofireOn < 1 ? 0 : Global.Config.AutofireOn;
	//		Off = Global.Config.AutofireOff < 1 ? 0 : Global.Config.AutofireOff;
	//	}

	//	public AutoFireStickyXorAdapter()
	//	{
	//		//On = Global.Config.AutofireOn < 1 ? 0 : Global.Config.AutofireOn;
	//		//Off = Global.Config.AutofireOff < 1 ? 0 : Global.Config.AutofireOff;
	//		On = 1;
	//		Off = 1;
	//	}

	//	public bool IsPressed(string button)
	//	{
	//		return this[button];
	//	}

	//	public bool this[string button]
	//	{
	//		get
	//		{
	//			var source = Source[button];

	//			if (_stickySet.Contains(button))
	//			{
	//				var lagcount = 0;
	//				if (Global.Emulator.CanPollInput() && Global.Config.AutofireLagFrames)
	//				{
	//					lagcount = Global.Emulator.AsInputPollable().LagCount;
	//				}

	//				var a = ((Global.Emulator.Frame - lagcount) - (buttonStarts[button] - lagStarts[button])) % (On + Off);
	//				if (a < On)
	//				{
	//					return source ^= true;
	//				}
	//				else
	//				{
	//					return source ^= false;
	//				}
	//			}

	//			return source;
	//		}

	//		set
	//		{
	//			throw new InvalidOperationException();
	//		}
	//	}

	//	public ControllerDefinition Type { get { return Source.Type; } set { throw new InvalidOperationException(); } }
	//	public bool Locked { get; set; } // Pretty much a hack, 

	//	// dumb passthrough for floats, because autofire doesn't care about them
	//	public float GetFloat(string name)
	//	{
	//		return Source.GetFloat(name);
	//	}

	//	public void SetSticky(string button, bool isSticky)
	//	{
	//		if (isSticky)
	//		{
	//			_stickySet.Add(button);
	//			buttonStarts.Add(button, Global.Emulator.Frame);

	//			if (Global.Emulator.CanPollInput())
	//			{
	//				lagStarts.Add(button, Global.Emulator.AsInputPollable().LagCount);
	//			}
	//			else
	//			{
	//				lagStarts.Add(button, 0);
	//			}
	//		}
	//		else
	//		{
	//			_stickySet.Remove(button);
	//			buttonStarts.Remove(button);
	//			lagStarts.Remove(button);
	//		}
	//	}

	//	public bool IsSticky(string button)
	//	{
	//		return this._stickySet.Contains(button);
	//	}

	//	public HashSet<string> CurrentStickies
	//	{
	//		get
	//		{
	//			return this._stickySet;
	//		}
	//	}

	//	public void ClearStickies()
	//	{
	//		_stickySet.Clear();
	//		buttonStarts.Clear();
	//		lagStarts.Clear();
	//	}

	//	public void MassToggleStickyState(List<string> buttons)
	//	{
	//		foreach (var button in buttons.Where(button => !_justPressed.Contains(button)))
	//		{
	//			if (_stickySet.Contains(button))
	//			{
	//				_stickySet.Remove(button);
	//			}
	//			else
	//			{
	//				_stickySet.Add(button);
	//			}
	//		}

	//		_justPressed = buttons;
	//	}

	//	/// <summary>
	//	/// Determines if a sticky is current mashing the button itself,
	//	/// If sticky is not set then false, if set, it returns true if the Source is not pressed, else false
	//	/// </summary>
	//	public bool StickyIsInEffect(string button)
	//	{
	//		if (Source.IsPressed(button))
	//		{
	//			return false;
	//		}

	//		return (IsPressed(button)); // Shortcut logic since we know the Source isn't pressed, Ispressed can only return true if the autofire sticky is in effect for this frame
	//	}

	//	private List<string> _justPressed = new List<string>();
	//}

	// commenting this out, it breaks the autofire hotkey
	public class AutoFireStickyXorAdapter : IController, ISticky
	{
		// TODO: Change the AutoHold adapter to be one of these, with an 'Off' value of 0?
		// Probably would have slightly lower performance, but it seems weird to have such a similar class that is only used once.
		private int On;
		private int Off;
		public void SetOnOffPatternFromConfig()
		{
			On = Global.Config.AutofireOn < 1 ? 0 : Global.Config.AutofireOn;
			Off = Global.Config.AutofireOff < 1 ? 0 : Global.Config.AutofireOff;
		}

		private WorkingDictionary<string, AutoPatternBool> _boolPatterns = new WorkingDictionary<string, AutoPatternBool>();
		private WorkingDictionary<string, AutoPatternFloat> _floatPatterns = new WorkingDictionary<string, AutoPatternFloat>();

		public AutoFireStickyXorAdapter()
		{
			On = 1; Off = 1;
		}

		public IController Source { get; set; }

		public ControllerDefinition Type
		{
			get { return Source.Type; }
		}

		public bool Locked { get; set; } // Pretty much a hack, 

		public bool IsPressed(string button)
		{
			return this[button];
		}

		public void SetFloat(string name, float? value, AutoPatternFloat pattern = null)
		{
			if (value.HasValue)
			{
				if (pattern == null)
					pattern = new AutoPatternFloat(value.Value, On, 0, Off);
				_floatPatterns[name] = pattern;
			}
			else
			{
				_floatPatterns.Remove(name);
			}
		}

		public float GetFloat(string name)
		{
			if (_floatPatterns.ContainsKey(name))
				return _floatPatterns[name].PeekNextValue();

			if (Source == null)
				return 0;

			return Source.GetFloat(name);
		}

		public void ClearStickyFloats()
		{
			_floatPatterns.Clear();
		}

		public bool this[string button]
		{
			get
			{
				var source = Source[button];
				bool patternValue = false;
				if (_boolPatterns.ContainsKey(button))
				{ // I can't figure a way to determine right here if it should Peek or Get.
					patternValue = _boolPatterns[button].PeekNextValue();
				}
				source ^= patternValue;

				return source;
			}
		}

		/// <summary>
		/// Determines if a sticky is current mashing the button itself,
		/// If sticky is not set then false, if set, it returns true if the Source is not pressed, else false
		/// </summary>
		public bool StickyIsInEffect(string button)
		{
			if (IsSticky(button))
			{
				return !Source.IsPressed(button);
			}

			return false;
		}

		public void SetSticky(string button, bool isSticky, AutoPatternBool pattern = null)
		{
			if (isSticky)
			{
				if (pattern == null)
					pattern = new AutoPatternBool(On, Off);
				_boolPatterns[button] = pattern;
			}
			else
			{
				_boolPatterns.Remove(button);
			}
		}

		public void Unset(string button)
		{
			_boolPatterns.Remove(button);
			_floatPatterns.Remove(button);
		}

		public bool IsSticky(string button)
		{
			return _boolPatterns.ContainsKey(button) || _floatPatterns.ContainsKey(button);
		}

		public HashSet<string> CurrentStickies
		{
			get
			{
				return new HashSet<string>(_boolPatterns.Keys);
			}
		}

		public void ClearStickies()
		{
			_boolPatterns.Clear();
			_floatPatterns.Clear();
		}

		public void IncrementLoops(bool lagged)
		{
			for (int i = 0; i < _boolPatterns.Count; i++)
				_boolPatterns.ElementAt(i).Value.GetNextValue(lagged);
			for (int i = 0; i < _floatPatterns.Count; i++)
				_floatPatterns.ElementAt(i).Value.GetNextValue(lagged);
		}

		private List<string> _justPressed = new List<string>();
		public void MassToggleStickyState(List<string> buttons)
		{
			foreach (var button in buttons.Where(button => !_justPressed.Contains(button)))
			{
				if (_boolPatterns.ContainsKey(button))
					SetSticky(button, false);
				else
					SetSticky(button, true);
			}

			_justPressed = buttons;
		}
	}

	/// <summary>
	/// Just copies source to sink, or returns whatever a NullController would if it is disconnected. useful for immovable hardpoints.
	/// </summary>
	public class CopyControllerAdapter : IController
	{
		public IController Source { get; set; }

		private readonly NullController _null = new NullController();

		private IController Curr
		{
			get
			{
				if (Source == null)
				{
					return _null;
				}
				else
				{
					return Source;
				}
			}
		}

		public ControllerDefinition Type
		{
			get { return Curr.Type; }
		}

		public bool this[string button]
		{
			get { return Curr[button]; }
		}

		public bool IsPressed(string button)
		{
			return Curr.IsPressed(button);
		}

		public float GetFloat(string name)
		{
			return Curr.GetFloat(name);
		}
	}

	/// <summary>
	/// Used to pass into an Override method to manage the logic overriding input
	/// This only works with bool buttons!
	/// </summary>
	public class OverrideAdaptor : IController
	{
		private readonly Dictionary<string, bool> _overrides = new Dictionary<string, bool>();
		private readonly Dictionary<string, float> _floatOverrides = new Dictionary<string, float>();
		private readonly List<string> _inverses = new List<string>();

		public bool this[string button]
		{
			get
			{
				if (_overrides.ContainsKey(button))
				{
					return _overrides[button];
				}

				throw new InvalidOperationException();
			}

			set
			{
				if (_overrides.ContainsKey(button))
				{
					_overrides[button] = value;
				}
				else
				{
					_overrides.Add(button, value);
				}
			}
		}

		public ControllerDefinition Type { get; set; }

		public IEnumerable<string> Overrides
		{
			get
			{
				foreach (var kvp in _overrides)
				{
					yield return kvp.Key;
				}
			}
		}

		public IEnumerable<string> FloatOverrides
		{
			get
			{
				foreach (var kvp in _floatOverrides)
				{
					yield return kvp.Key;
				}
			}
		}

		public IEnumerable<string> InversedButtons
		{
			get
			{
				foreach (var name in _inverses)
				{
					yield return name;
				}
			}
		}

		public void SetFloat(string name, float value)
		{
			if (_floatOverrides.ContainsKey(name))
			{
				_floatOverrides[name] = value;
			}
			else
			{
				_floatOverrides.Add(name, value);
			}
		}

		public float GetFloat(string name)
		{
			if (_floatOverrides.ContainsKey(name))
			{
				return _floatOverrides[name];
			}

			return 0.0F;
		}

		public bool IsPressed(string button) { return this[button]; }

		public void SetButton(string button, bool value)
		{
			this[button] = value;
			_inverses.Remove(button);
		}

		public void UnSet(string button)
		{
			_overrides.Remove(button);
			_inverses.Remove(button);
		}

		public void SetInverse(string button)
		{
			_inverses.Add(button);
		}

		public void FrameTick()
		{
			_overrides.Clear();
			_floatOverrides.Clear();
			_inverses.Clear();
		}
	}
}