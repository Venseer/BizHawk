﻿using System.Linq;
using System.Text;

using BizHawk.Emulation.Common;
using System.Collections.Generic;

namespace BizHawk.Client.Common
{
	public class Bk2LogEntryGenerator : ILogEntryGenerator
	{
		private readonly Bk2MnemonicConstants Mnemonics = new Bk2MnemonicConstants();
		private readonly Bk2FloatConstants FloatLookup = new Bk2FloatConstants();

		private IController _source;
		private readonly string _logKey = string.Empty;

		public Bk2LogEntryGenerator(string logKey)
		{
			_logKey = logKey;
		}

		public IMovieController MovieControllerAdapter
		{
			get
			{
				return new Bk2ControllerAdapter(_logKey);
			}
		}

		#region ILogEntryGenerator Implementation

		public void SetSource(IController source)
		{
			_source = source;
		}

		public string GenerateInputDisplay()
		{
			var le = GenerateLogEntry();
			if (le == EmptyEntry)
			{
				return string.Empty;
			}
			
			string neutral = "    0,";
			if (_source.Type.FloatRanges.Count > 0)
				neutral = _source.Type.FloatRanges[0].Mid.ToString().PadLeft(5, ' ') + ',';
			return le
				.Replace(".", " ")
				.Replace("|", "")
				.Replace(neutral, "      "); //zero 04-aug-2015 - changed from a 2-dimensional type string to support emptying out the one-dimensional PSX disc select control
		}

		public bool IsEmpty
		{
			get
			{
				return EmptyEntry == GenerateLogEntry();
			}
		}

		public string EmptyEntry
		{
			get
			{
				return CreateLogEntry(createEmpty: true);
			}
		}

		public string GenerateLogEntry()
		{
			return CreateLogEntry();
		}

		#endregion

		public string GenerateLogKey()
		{
			var sb = new StringBuilder();
			sb.Append("LogKey:");

			foreach (var group in _source.Type.ControlsOrdered.Where(c => c.Any()))
			{
				sb.Append("#");
				foreach (var button in group)
				{
					sb
						.Append(button)
						.Append('|');
				}
			}

			return sb.ToString();
		}

		public Dictionary<string, string> Map()
		{
			var dict = new Dictionary<string, string>();
			foreach (var group in _source.Type.ControlsOrdered.Where(c => c.Any()))
			{
				foreach (var button in group)
				{
					if (_source.Type.BoolButtons.Contains(button))
					{
						dict.Add(button, Mnemonics[button].ToString());
					}
					else if (_source.Type.FloatControls.Contains(button))
					{
						dict.Add(button, FloatLookup[button]);
					}
				}
			}

			return dict;
		}

		private string CreateLogEntry(bool createEmpty = false)
		{
			var sb = new StringBuilder();
			sb.Append('|');

			foreach (var group in _source.Type.ControlsOrdered)
			{
				if (group.Any())
				{
					foreach (var button in group)
					{
						if (_source.Type.FloatControls.Contains(button))
						{
							if (createEmpty)
							{
								sb.Append("    0,");
							}
							else
							{
								var val = (int)_source.GetFloat(button);
								sb.Append(val.ToString().PadLeft(5, ' ')).Append(',');
							}
						}
						else if (_source.Type.BoolButtons.Contains(button))
						{
							if (createEmpty)
							{
								sb.Append('.');
							}
							else
							{
								sb.Append(_source.IsPressed(button) ? Mnemonics[button] : '.');
							}
						}
					}

					sb.Append('|');
				}
			}

			return sb.ToString();
		}
	}
}
