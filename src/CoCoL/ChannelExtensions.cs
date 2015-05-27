﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CoCoL
{
	/// <summary>
	/// This static class provides various extension methods for
	/// simplifying the use of channels other than async
	/// </summary>
	public static class ChannelExtensions
	{
		/// <summary>
		/// Single-shot variable that is set if we are running under the Mono runtime
		/// </summary>
		private static readonly bool IsRunningMono = Type.GetType ("Mono.Runtime") != null;

		/// <summary>
		/// Blocking wait for a task, equivalent to calling Task.Wait(),
		/// but works around a race in Mono that causes Wait() to hang
		/// </summary>
		/// <param name="t">The task to wait for</param>
		/// <returns>The task</returns>
		public static Task<T> WaitForTask<T>(this Task<T> task)
		{
			// Mono has a race when waiting for a
			// task to complete, this workaround
			// ensures that the wait call does not hang
			if (IsRunningMono)
			{
				using (var lck = new System.Threading.ManualResetEventSlim(false))
				{
					task.ContinueWith(x => lck.Set());
					// This ensures we never return with 
					// an incomplete task, but may casue
					// some spin waiting
					while (!task.IsCompleted)
						lck.Wait();
				}
			}
			else
			{
				// Don't throw the exception here
				// let the caller access the task
				try { task.Wait(); } 
				catch {	}
			}

			return task;
		}

		/// <summary>
		/// Blocking wait for a task, equivalent to calling Task.Wait(),
		/// but works around a race in Mono that causes Wait() to hang
		/// </summary>
		/// <param name="t">The task to wait for</param>
		/// <returns>The task</returns>
		public static Task WaitForTask(this Task task)
		{
			// Mono has a race when waiting for a
			// task to complete, this workaround
			// ensures that the wait call does not hang
			if (IsRunningMono)
			{
				using (var lck = new System.Threading.ManualResetEventSlim(false))
				{
					task.ContinueWith(x => lck.Set());
					// This ensures we never return with 
					// an incomplete task, but may casue
					// some spin waiting
					while (!task.IsCompleted)
						lck.Wait();
				}
			}
			else
			{
				// Don't throw the exception here
				// let the caller access the task
				try { task.Wait(); } 
				catch {	}
			}

			return task;
		}

		#region Avoid compile warnings when using the write method in fire-n-forget mode
		/// <summary>
		/// Write to the channel in a blocking manner
		/// </summary>
		/// <param name="value">The value to write into the channel</param>
		/// <param name="self">The channel to read from</param>
		/// <param name="timeout">The maximum time to wait for an available slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		public static void WriteNoWait<T>(this IWriteChannel<T> self, T value)
		{
			ThreadPool.QueueItem(() =>
				{
					self.WriteAsync(value, Timeout.Infinite);
				});
		}

		/// <summary>
		/// Write to the channel in a blocking manner
		/// </summary>
		/// <param name="value">The value to write into the channel</param>
		/// <param name="self">The channel to read from</param>
		/// <param name="timeout">The maximum time to wait for an available slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		public static void WriteNoWait<T>(this IWriteChannel<T> self, T value, TimeSpan timeout)
		{
			ThreadPool.QueueItem(() =>
				{
					self.WriteAsync(value, timeout);
				});
		}
		#endregion

		#region Blocking channel usage
		/// <summary>
		/// Read from the channel in a blocking manner
		/// </summary>
		/// <param name="self">The channel to read from</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from the channel</returns>
		public static T Read<T>(this IReadChannel<T> self)
		{
			try
			{
				return self.ReadAsync(Timeout.Infinite).WaitForTask().Result;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;
				
				throw;
			}
		}

		/// <summary>
		/// Read from the channel in a blocking manner
		/// </summary>
		/// <param name="self">The channel to read from</param>
		/// <param name="timeout">The maximum time to wait for a value</param>
		/// <returns>>The value read from the channel</returns>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		public static T Read<T>(this IReadChannel<T> self, TimeSpan timeout)
		{
			try
			{
				return self.ReadAsync(timeout).WaitForTask().Result;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}
		}

		/// <summary>
		/// Read from the channel in a probing manner
		/// </summary>
		/// <param name="self">The channel to read from</param>
		/// <param name="result">The read result</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>True if the read succeeded, false otherwise</returns>
		public static bool TryRead<T>(this IReadChannel<T> self, out T result)
		{
			var res = self.ReadAsync(Timeout.Immediate);

			if (res.Exception != null)
			{
				result = default(T);
				return false;
			}
			else
			{
				result = res.Result;
				return true;
			}
		}

		/// <summary>
		/// Write to the channel in a blocking manner
		/// </summary>
		/// <param name="value">The value to write into the channel</param>
		/// <param name="self">The channel to read from</param>
		/// <param name="timeout">The maximum time to wait for an available slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		public static void Write<T>(this IWriteChannel<T> self, T value)
		{
			var res = self.WriteAsync(value, Timeout.Infinite).WaitForTask();

			if (res.Exception != null)
			{
				if (res.Exception is AggregateException && ((AggregateException)res.Exception).Flatten().InnerExceptions.Count == 1)
					throw ((AggregateException)res.Exception).InnerException;
				
				throw res.Exception;
			}
		}

		/// <summary>
		/// Write to the channel in a blocking manner
		/// </summary>
		/// <param name="value">The value to write into the channel</param>
		/// <param name="self">The channel to read from</param>
		/// <param name="timeout">The maximum time to wait for an available slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		public static void Write<T>(this IWriteChannel<T> self, T value, TimeSpan timeout)
		{
			var res = self.WriteAsync(value, timeout).WaitForTask();

			if (res.Exception != null)
			{
				if (res.Exception is AggregateException && ((AggregateException)res.Exception).Flatten().InnerExceptions.Count == 1)
					throw ((AggregateException)res.Exception).InnerException;

				throw res.Exception;
			}
		}		

		/// <summary>
		/// Write to the channel in a probing manner
		/// </summary>
		/// <param name="value">The value to write into the channel</param>
		/// <param name="self">The channel to read from</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>True if the write succeeded, false otherwise</returns>
		public static bool TryWrite<T>(this IWriteChannel<T> self, T value)
		{
			return self.WriteAsync(value, Timeout.Immediate).Exception == null;
		}
		#endregion

		#region Blocking multi-channel usage
		/// <summary>
		/// Read from the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static MultisetResult<T> ReadFromAny<T>(this MultiChannelSet<T> self)
		{
			try
			{
				return self.ReadFromAnyAsync(Timeout.Infinite).WaitForTask().Result;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}
		}

		/// <summary>
		/// Read from the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static T ReadFromAny<T>(this MultiChannelSet<T> self, out IChannel<T> channel)
		{
			try
			{
				var res = self.ReadFromAnyAsync(Timeout.Infinite).WaitForTask().Result;
				channel = res.Channel;
				return res.Value;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}

		}

		/// <summary>
		/// Read from the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <param name="timeout">The maximum time to wait for a value</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static T ReadFromAny<T>(this MultiChannelSet<T> self, out IChannel<T> channel, TimeSpan timeout)
		{
			try
			{
				var res = self.ReadFromAnyAsync(timeout).WaitForTask().Result;
				channel = res.Channel;
				return res.Value;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}
		}

		/// <summary>
		/// Read from the channel set in a probing manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static bool TryReadFromAny<T>(this MultiChannelSet<T> self, out T value, out IChannel<T> channel)
		{
			var res = self.ReadFromAnyAsync(Timeout.Immediate).WaitForTask();

			if (res.Exception != null)
			{
				channel = null;
				value = default(T);
				return false;
			}
			else
			{
				channel = res.Result.Channel;
				value = res.Result.Value;
				return true;
			}
		}

		/// <summary>
		/// Read from the channel set in a probing manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static bool TryReadFromAny<T>(this MultiChannelSet<T> self, out T value)
		{
			IChannel<T> dummy;
			return TryReadFromAny<T>(self, out value, out dummy);
		}

		/// <summary>
		/// Read from the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <param name="timeout">The maximum time to wait for a value</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <returns>The value read from a channel</returns>
		public static MultisetResult<T> ReadFromAny<T>(this MultiChannelSet<T> self, TimeSpan timeout)
		{
			try
			{
				return self.ReadFromAnyAsync(timeout).WaitForTask().Result;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}
		}

		/// <summary>
		/// Write to the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <param name="value">The value to write into the channel</param>
		public static IChannel<T> WriteToAny<T>(this MultiChannelSet<T> self, T value)
		{
			return WriteToAny(self, value, Timeout.Infinite);
		}

		/// <summary>
		/// Write to the channel set in a blocking manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <param name="timeout">The maximum time to wait for a slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <param name="value">The value to write into the channel</param>
		public static IChannel<T> WriteToAny<T>(this MultiChannelSet<T> self, T value, TimeSpan timeout)
		{
			try
			{
				return self.WriteToAnyAsync(value, timeout).WaitForTask().Result;
			}
			catch(AggregateException aex)
			{
				if (aex.Flatten().InnerExceptions.Count == 1)
					throw aex.InnerException;

				throw;
			}
		}

		/// <summary>
		/// Read from the channel set in a probing manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <param name="timeout">The maximum time to wait for a slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <param name="value">The value to write into the channel</param>
		public static bool TryWriteToAny<T>(this MultiChannelSet<T> self, T value)
		{
			IChannel<T> dummy;
			return TryWriteToAny(self, value, out dummy);
		}

		/// <summary>
		/// Read from the channel set in a probing manner
		/// </summary>
		/// <param name="self">The channels to read from</param>
		/// <param name="channel">The channel written to</param>
		/// <param name="timeout">The maximum time to wait for a slot</param>
		/// <typeparam name="T">The channel data type parameter.</typeparam>
		/// <param name="value">The value to write into the channel</param>
		public static bool TryWriteToAny<T>(this MultiChannelSet<T> self, T value, out IChannel<T> channel)
		{
			var res = self.WriteToAnyAsync(value, Timeout.Immediate).WaitForTask();

			if (res.Exception == null)
			{
				channel = res.Result;
				return true;
			}
			else
			{
				channel = null;
				return false;
			}
		}
		#endregion

		#region Readable and Writeable casting
		/// <summary>
		/// Returns the channel as a read channel
		/// </summary>
		/// <returns>The channel as a read channel</returns>
		/// <param name="channel">The channel to cast.</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static IReadChannel<T> AsRead<T>(this IChannel<T> channel)
		{
			return channel;
		}

		/// <summary>
		/// Returns the channel as a write channel
		/// </summary>
		/// <returns>The channel as a write channel</returns>
		/// <param name="channel">The channel to cast.</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static IWriteChannel<T> AsWrite<T>(this IChannel<T> channel)
		{
			return channel;
		}

		/// <summary>
		/// Returns the channel as a read channel
		/// </summary>
		/// <returns>The channel as a read channel</returns>
		/// <param name="channel">The channel to cast.</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static IBlockingReadableChannel<T> AsRead<T>(this IBlockingChannel<T> channel)
		{
			return channel;
		}

		/// <summary>
		/// Returns the channel as a write channel
		/// </summary>
		/// <returns>The channel as a write channel</returns>
		/// <param name="channel">The channel to cast.</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static IBlockingWriteableChannel<T> AsWrite<T>(this IBlockingChannel<T> channel)
		{
			return channel;
		}
		#endregion

		#region Operations on lists of channels
		/// <summary>
		/// Retires all channels in the list
		/// </summary>
		/// <param name="list">The list of channels to retire</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static void Retire<T>(this IEnumerable<IChannel<T>> list)
		{
			foreach (var c in list)
				c.Retire();
		}

		/// <summary>
		/// Retires all channels in the list
		/// </summary>
		/// <param name="list">The list of channels to retire</param>
		/// <typeparam name="T">The channel data type.</typeparam>
		public static void Retire<T>(this IEnumerable<IBlockingChannel<T>> list)
		{
			foreach (var c in list)
				c.Retire();
		}
		#endregion
	}
}

