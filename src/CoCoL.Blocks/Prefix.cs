﻿using System;
using System.Threading.Tasks;

namespace CoCoL.Blocks
{
	/// <summary>
	/// The prefix process outputs a value a number of times,
	/// and then becomes an identity process
	/// </summary>
	public class Prefix<T> : BlockBase
	{
		private IReadChannel<T> m_input;
		private IWriteChannel<T> m_output;
		private T m_value;
		private long m_repeat;

		public Prefix(IReadChannel<T> input, IWriteChannel<T> output, T value, long repeat = 1)
		{
			if (input == null)
				throw new ArgumentNullException("input");
			if (output == null)
				throw new ArgumentNullException("output");

			m_input = input;
			m_output = output;
			m_value = value;
			m_repeat = repeat;
		}

		public async override Task RunAsync()
		{
			try
			{
				while(m_repeat-- > 0)
					await m_output.WriteAsync(m_value);
					
				while (true)
					await m_output.WriteAsync(await m_input.ReadAsync());
			}
			catch (RetiredException)
			{
				m_input.Retire();
				m_output.Retire();
			}
		}

	}
}

