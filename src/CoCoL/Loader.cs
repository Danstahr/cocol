﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoCoL
{
	/// <summary>
	/// Helper class for loading statically denfined processes
	/// </summary>
	public static class Loader
	{
		/// <summary>
		/// Finds all classes marked as Process and launches them
		/// </summary>
		/// <param name="asm">The assemblies to examine.</param>
		public static void StartFromAssembly(params Assembly[] asm)
		{
			StartFromAssembly(asm.AsEnumerable());
		}

		/// <summary>
		/// Finds all classes marked as Process and launches them
		/// </summary>
		/// <param name="asm">The assemblies to examine.</param>
		public static void StartFromAssembly(IEnumerable<Assembly> asm)
		{
			var c = (from a in asm
			         select StartFromTypes(a.GetTypes())).Sum();

			if (c == 0)
				throw new Exception("No process found in given assemblies");
		}

		/// <summary>
		/// Helper iterator to repeatedly call a function, like Enumerator.Range, but for Int64
		/// </summary>
		/// <param name="count">The number of repetitions to perform</param>
		/// <param name="op">The method to call, which receives the index</param>
		/// <typeparam name="T">The return type parameter.</typeparam>
		public static IEnumerable<T> Each<T>(long count, Func<long, T> op)
		{
			for (var i = 0L; i < count; i++)
				yield return op(i);
		}

		/// <summary>
		/// Starts all process found in the given types
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="types">The types to examine</param>
		public static int StartFromTypes(params Type[] types)
		{
			return StartFromTypes(types.AsEnumerable());
		}

		/// <summary>
		/// Starts all process found in the given types
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="types">The types to examine</param>
		public static int StartFromTypes(IEnumerable<Type> types)
		{
			var count = 0;
			foreach (var c in 
				from n in types
				let isRunable = typeof(IProcess).IsAssignableFrom(n)
				let decorator = n.GetCustomAttributes(typeof(ProcessAttribute), true).FirstOrDefault() as ProcessAttribute
				where n.IsClass && isRunable && n.GetConstructor(new Type[0]) != null
				select new { Class = n, Decorator = decorator ?? new ProcessAttribute() })
			{
				if (typeof(IAsyncProcess).IsAssignableFrom(c.Class)) 
					count += StartFromProcesses(Each(c.Decorator.ProcessCount, x => ((IAsyncProcess)Activator.CreateInstance(c.Class))));
				else
					count += StartFromProcesses(Each(c.Decorator.ProcessCount, x => ((IProcess)Activator.CreateInstance(c.Class))));

				// Register static events
				SetupEvents(c.Class);
			}	
				
			return count;
		}

		/// <summary>
		/// Helper class for repeatedly calling a method after a channel has been read
		/// </summary>
		private class OnReadHandler<T>
		{
			/// <summary>
			/// The set to read from
			/// </summary>
			private MultiChannelSet<T> m_set;
			/// <summary>
			/// The timeout value
			/// </summary>
			private TimeSpan m_timeout;
			/// <summary>
			/// The callback method, invoked on each read
			/// </summary>
			private Action<Task<MultisetResult<T>>> m_callback;

			/// <summary>
			/// Initializes a new instance of the <see cref="CoCoL.Loader+OnReadHandler`1"/> class.
			/// </summary>
			/// <param name="channels">The channels to process.</param>
			/// <param name="priority">The channel selection priority.</param>
			/// <param name="timeout">The time to wait for a read result.</param>
			/// <param name="callback">The delegate to call when the data is available.</param>
			public OnReadHandler(string[] channels, MultiChannelPriority priority, TimeSpan timeout, Action<Task<MultisetResult<T>>> callback)
			{
				if (channels == null)
					throw new ArgumentNullException("channels");

				if (channels.Length <= 0)
					throw new ArgumentException("channels");
				
				m_set = new MultiChannelSet<T>(channels.Select(x => (IChannel<T>)ChannelManager.GetChannel<T>(x)).ToArray(), priority);
				m_callback = callback;
				m_timeout = timeout;
				RunHandler();
			}
				
			/// <summary>
			/// The callback delegate method
			/// </summary>
			/// <param name="item">The channel result</param>
			public async void RunHandler()
			{
				while(true)
				{
					try
					{
							var t = m_set.ReadFromAnyAsync(m_timeout);
							await t;
							m_callback(t);
					}
					catch(RetiredException)
					{
						// Stop reading
						return;
					}
					catch(Exception ex)
					{
						System.Diagnostics.Trace.WriteLine(ex);
					}
				}
			}
		}
			
		/// <summary>
		/// Creates a read handler from a method marked with OnRead.
		/// </summary>
		/// <returns>The read handler.</returns>
		/// <param name="attr">The attribute on the method.</param>
		/// <param name="m">The method to call.</param>
		/// <param name="instance">The object instance to register the callback on.</param>
		private static object CreateReadHandler(OnReadAttribute attr, MethodInfo m, object instance)
		{
			var gentype = m.GetParameters()[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition().GetGenericArguments()[0];
			var rht = typeof(OnReadHandler<>).MakeGenericType(gentype);
			var cbt = typeof(Task<>).MakeGenericType(typeof(MultiChannelSet<>).MakeGenericType(gentype));

			var cb = Delegate.CreateDelegate(cbt, instance, m);

			return Activator.CreateInstance(rht, new object[] { attr.Channels, attr.Priority, attr.Timeout, cb });
		}

		/// <summary>
		/// Registers repeated callbacks for methods in the class
		/// </summary>
		/// <param name="o">The item to register callbacks on. If o is a Type, then static methods for the type are set up</param>
		public static void SetupEvents(object o)
		{
			if (o == null)
				throw new ArgumentNullException("o");

			var staticMethodsOnly = o is Type;
			var t = staticMethodsOnly ? o as Type : o.GetType();
			var ms = t.GetMethods((staticMethodsOnly ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic);

			var methods = 
				from n in ms
				let decorator = n.GetCustomAttributes(typeof(OnReadAttribute), true).FirstOrDefault() as OnReadAttribute
				let parameters = n.GetParameters()
					where 
						decorator != null && 
						decorator.Channels != null && 
						decorator.Channels.Length > 0 && 
						parameters.Length == 1 && 
						parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(Task<>) && 
						parameters[0].ParameterType.GetGenericArguments().Length == 1 && 
						parameters[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(MultiChannelSet<>)
				
				select new { Method = n, Decorator = decorator };

			foreach (var m in methods)
				CreateReadHandler(m.Decorator, m.Method, staticMethodsOnly ? null : o);
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static int StartFromProcesses(IEnumerable<IProcess> processes)
		{
			var count = 0;
			foreach (var p in processes)
			{
				count++;
				ThreadPool.QueueItem(p.Run);

				SetupEvents(p);

			}

			return count;
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static int StartFromProcesses(IEnumerable<IAsyncProcess> processes)
		{
			var count = 0;
			foreach (var p in processes)
			{
				count++;
				ThreadPool.QueueItem(() => { p.RunAsync(); });

				SetupEvents(p);
			}

			return count;
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static Task<int> StartAsync(this IEnumerable<IProcess> processes)
		{
			return System.Threading.Tasks.Task.Run(() => StartFromProcesses(processes));
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static Task<int> StartAsync(this IEnumerable<IAsyncProcess> processes)
		{
			return System.Threading.Tasks.Task.Run(() => StartFromProcesses(processes));
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static int Start(this IEnumerable<IProcess> processes)
		{
			return StartFromProcesses(processes);
		}

		/// <summary>
		/// Starts processes by scheduling their run method for execution
		/// </summary>
		/// <returns>The number of processes started</returns>
		/// <param name="processes">The list of process instances to start</param>
		public static int Start(this IEnumerable<IAsyncProcess> processes)
		{
			return StartFromProcesses(processes);
		}
	}
}

