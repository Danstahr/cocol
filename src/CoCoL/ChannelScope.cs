﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace CoCoL
{
	/// <summary>
	/// Implementation of a nested scope for assigning channel names
	/// </summary>
	public class ChannelScope : IDisposable
	{
		/// <summary>
		/// The root scope, where all other scopes descend from
		/// </summary>
		public static readonly ChannelScope Root;

		/// <summary>
		/// The lock object
		/// </summary>
		private static readonly object __lock;

		/// <summary>
		/// Static initializer to control the creation order
		/// </summary>
		static ChannelScope()
		{
			__lock = new object();
			Root = new ChannelScope(null, true);
		}
			

		/// <summary>
		/// True if this instance is disposed, false otherwise
		/// </summary>
		private bool m_isDisposed = false;

		/// <summary>
		/// The parent scope, or null if this is the root scope
		/// </summary>
		/// <value>The parent scope.</value>
		public ChannelScope ParentScope { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this scope is isolated, meaning that it does not inherit from the parent scope.
		/// </summary>
		/// <value><c>true</c> if this instance isolated; otherwise, <c>false</c>.</value>
		public bool Isolated { get; private set; }

		/// <summary>
		/// The local storage for channels
		/// </summary>
		private Dictionary<string, IRetireAbleChannel> m_lookup = new Dictionary<string, IRetireAbleChannel>();

		/// <summary>
		/// The key used to assign the current scope into the current call-context
		/// </summary>
		private const string LOGICAL_CONTEXT_KEY = "CoCoL:AutoWireScope";

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.ChannelScope"/> class that derives from the current scope.
		/// </summary>
		public ChannelScope()
			: this(ChannelScope.Current, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.ChannelScope"/> class that derives from the current scope.
		/// </summary>
		/// <param name="isolated"><c>True</c> if this is an isolated scope, <c>false</c> otherwise</param>
		public ChannelScope(bool isolated)
			: this(ChannelScope.Current, isolated)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.ChannelScope"/> class that derives from a parent scope.
		/// </summary>
		/// <param name="parent">The parent scope.</param>
		/// <param name="isolated"><c>True</c> if this is an isolated scope, <c>false</c> otherwise</param>
		private ChannelScope(ChannelScope parent, bool isolated)
		{
			ParentScope = parent;
			Isolated = isolated;
			Current = this;
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The or create.</returns>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="datatype">The type of data communicated through the channel.</param>
		/// <param name="buffersize">The size of the channel buffer.</param>
		public IRetireAbleChannel GetOrCreate(string name, Type datatype, int buffersize = 0)
		{
			return (IRetireAbleChannel)typeof(ChannelScope).GetMethod("GetOrCreate", new Type[] { typeof(string), typeof(int) })
				.MakeGenericMethod(datatype)
				.Invoke(this, new object[] {name, buffersize});
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="buffersize">The size of the channel buffer.</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		public IChannel<T> GetOrCreate<T>(string name, int buffersize = 0)
		{
			IRetireAbleChannel res;
			if (m_lookup.TryGetValue(name, out res))
				return (IChannel<T>)res;

			lock (__lock)
			{
				var cur = this;
				while (cur != null)
				{
					if (cur.m_lookup.TryGetValue(name, out res))
						return (IChannel<T>)res;

					if (Isolated)
						cur = null;
					else
						cur = cur.ParentScope;
				}

				var chan = ChannelManager.CreateChannelForScope<T>(name, buffersize);
				m_lookup.Add(name, chan);
				return chan;
			}
		}

		/// <summary>
		/// Injects a channel into the current scope.
		/// </summary>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="channel">The channel to inject.</param>
		public void InjectChannel(string name, IRetireAbleChannel channel)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name");
			if (channel == null)
				throw new ArgumentNullException("channel");
			
			lock (__lock)
				m_lookup[name] = channel;
		}

		#region IDisposable implementation

		/// <summary>
		/// Releases all resource used by the <see cref="CoCoL.ChannelScope"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="CoCoL.ChannelScope"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="CoCoL.ChannelScope"/> in an unusable state. After calling
		/// <see cref="Dispose"/>, you must release all references to the <see cref="CoCoL.ChannelScope"/> so the garbage
		/// collector can reclaim the memory that the <see cref="CoCoL.ChannelScope"/> was occupying.</remarks>
		public void Dispose()
		{
			lock (__lock)
			{
				if (this == Root)
					throw new InvalidOperationException("Cannot dispose the root scope");
				
				if (Current == this)
				{
					Current = this.ParentScope;

					// Disposal can be non-deterministic, so we walk the chain
					while (Current.m_isDisposed)
						Current = Current.ParentScope;
				}
				m_isDisposed = true;
				m_lookup = null;
			}
		}

		#endregion

#if PCL_BUILD
		private static bool __IsFirstUsage = true;
		private static ChannelScope __Current = null;
#endif

		public static ChannelScope Current
		{
			get 
			{
				lock (__lock)
				{
#if PCL_BUILD
					// TODO: Use AsyncLocal if targeting 4.6
					//var cur = new System.Threading.AsyncLocal<ChannelScope>();
					if (__IsFirstUsage)
					{
						__IsFirstUsage = false;
						System.Diagnostics.Debug.WriteLine("*Warning*: PCL does not provide a call context, so channel scoping does not work correctly for multithreaded use!");
					}

					var cur = __Current;
#else
					var cur = System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(LOGICAL_CONTEXT_KEY) as ChannelScope;
#endif
					if (cur == null)
						return Current = Root;
					else
						return cur;
				}
			}
			private set
			{
				lock (__lock)
				{
#if PCL_BUILD				
					__Current = value;
#else
					System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(LOGICAL_CONTEXT_KEY, value);
#endif
				}
			}
		}
	}
}

