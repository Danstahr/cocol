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
		protected static readonly object __lock;

		/// <summary>
		/// Lookup table for scopes
		/// </summary>
		protected static readonly Dictionary<string, ChannelScope> __scopes = new Dictionary<string, ChannelScope>();

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
		protected bool m_isDisposed = false;

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
		/// The key for this instance
		/// </summary>
		private readonly string m_instancekey = Guid.NewGuid().ToString("N");

		/// <summary>
		/// The local storage for channels
		/// </summary>
		protected Dictionary<string, IRetireAbleChannel> m_lookup = new Dictionary<string, IRetireAbleChannel>();

		/// <summary>
		/// The key used to assign the current scope into the current call-context
		/// </summary>
		protected const string LOGICAL_CONTEXT_KEY = "CoCoL:AutoWireScope";

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
		protected ChannelScope(bool isolated)
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
			lock (__lock)
				__scopes[m_instancekey] = this;
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The or create.</returns>
		/// <param name="attribute">The attribute describing the channel to create.</param>
		/// <param name="datatype">The type of data communicated through the channel.</param>
		public IRetireAbleChannel GetOrCreate(ChannelNameAttribute attribute, Type datatype)
		{
			return (IRetireAbleChannel)typeof(ChannelScope).GetMethod("GetOrCreate", new Type[] { typeof(ChannelNameAttribute) })
			   .MakeGenericMethod(datatype)
               .Invoke(this, new object[] { attribute });
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The or create.</returns>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="datatype">The type of data communicated through the channel.</param>
		/// <param name="buffersize">The size of the channel buffer.</param>
		/// <param name="maxPendingReaders">The maximum number of pending readers. A negative value indicates infinite</param>
		/// <param name="maxPendingWriters">The maximum number of pending writers. A negative value indicates infinite</param>
		/// <param name="pendingReadersOverflowStrategy">The strategy for dealing with overflow for read requests</param>
		/// <param name="pendingWritersOverflowStrategy">The strategy for dealing with overflow for write requests</param>
		/// <param name="broadcast"><c>True</c> will create the channel as a broadcast channel, the default <c>false</c> will create a normal channel</param>
		/// <param name="initialBroadcastBarrier">The number of readers required on the channel before sending the first broadcast, can only be used with broadcast channels</param>
		/// <param name="broadcastMinimum">The minimum number of readers required on the channel, before a broadcast can be performed, can only be used with broadcast channels</param>
		public IRetireAbleChannel GetOrCreate(string name, Type datatype, int buffersize = 0, int maxPendingReaders = -1, int maxPendingWriters = -1, QueueOverflowStrategy pendingReadersOverflowStrategy = QueueOverflowStrategy.Reject, QueueOverflowStrategy pendingWritersOverflowStrategy = QueueOverflowStrategy.Reject, bool broadcast = false, int initialBroadcastBarrier = -1, int broadcastMinimum = -1)
		{
			if (!broadcast && (initialBroadcastBarrier >= 0 || broadcastMinimum >= 0))
				throw new ArgumentException(string.Format("Cannot set \"{0}\" or \"{1}\" unless the channel is a broadcast channel", "initialBroadcastBarrier", "broadcastMinimum"));

			var attr = 
				broadcast
				? new BroadcastChannelNameAttribute(name, buffersize, ChannelNameScope.Local, maxPendingReaders, maxPendingWriters, pendingReadersOverflowStrategy, pendingWritersOverflowStrategy, initialBroadcastBarrier, broadcastMinimum)
				: new ChannelNameAttribute(name, buffersize, ChannelNameScope.Local, maxPendingReaders, maxPendingWriters, pendingReadersOverflowStrategy, pendingWritersOverflowStrategy);

			return GetOrCreate(
				attr, 
				datatype
			);
		}


		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="marker">The <see cref="ChannelNameMarker"/> of the channel to create.</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		public IChannel<T> GetOrCreate<T>(ChannelNameMarker marker)
		{
			return this.GetOrCreate<T>(marker.Attribute);
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="marker">The <see cref="ChannelNameMarker"/> of the channel to create.</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		public IChannel<T> GetOrCreate<T>(ChannelMarkerWrapper<T> marker)
		{
			return this.GetOrCreate<T>(marker.Attribute);
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="attribute">The attribute describing the channel.</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		public IChannel<T> GetOrCreate<T>(ChannelNameAttribute attribute)
		{
			if (attribute == null)
				throw new ArgumentNullException("attribute");

			lock (__lock)
			{
				var res = RecursiveLookup(attribute.Name);
				if (res != null)
					return (IChannel<T>)res;
				else
				{
					var chan = DoCreateChannel<T>(attribute);
					if (!string.IsNullOrWhiteSpace(attribute.Name))
						m_lookup.Add(attribute.Name, chan);
					return chan;
				}
			}
		}

		/// <summary>
		/// Gets or creates a channel
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="buffersize">The size of the channel buffer.</param>
		/// <param name="maxPendingReaders">The maximum number of pending readers. A negative value indicates infinite</param>
		/// <param name="maxPendingWriters">The maximum number of pending writers. A negative value indicates infinite</param>
		/// <param name="pendingReadersOverflowStrategy">The strategy for dealing with overflow for read requests</param>
		/// <param name="pendingWritersOverflowStrategy">The strategy for dealing with overflow for write requests</param>
		/// <param name="broadcast"><c>True</c> will create the channel as a broadcast channel, the default <c>false</c> will create a normal channel</param>
		/// <param name="initialBroadcastBarrier">The number of readers required on the channel before sending the first broadcast, can only be used with broadcast channels</param>
		/// <param name="broadcastMinimum">The minimum number of readers required on the channel, before a broadcast can be performed, can only be used with broadcast channels</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		public IChannel<T> GetOrCreate<T>(string name, int buffersize = 0, int maxPendingReaders = -1, int maxPendingWriters = -1, QueueOverflowStrategy pendingReadersOverflowStrategy = QueueOverflowStrategy.Reject, QueueOverflowStrategy pendingWritersOverflowStrategy = QueueOverflowStrategy.Reject, bool broadcast = false, int initialBroadcastBarrier = -1, int broadcastMinimum = -1)
		{
			if (!broadcast && (initialBroadcastBarrier >= 0 || broadcastMinimum >= 0))
				throw new ArgumentException(string.Format("Cannot set \"{0}\" or \"{1}\" unless the channel is a broadcast channel", "initialBroadcastBarrier", "broadcastMinimum"));

			var attr =
				broadcast
				? new BroadcastChannelNameAttribute(name, buffersize, ChannelNameScope.Local, maxPendingReaders, maxPendingWriters, pendingReadersOverflowStrategy, pendingWritersOverflowStrategy, initialBroadcastBarrier, broadcastMinimum)
				: new ChannelNameAttribute(name, buffersize, ChannelNameScope.Local, maxPendingReaders, maxPendingWriters, pendingReadersOverflowStrategy, pendingWritersOverflowStrategy);

			return GetOrCreate<T>(attr);
		}

		/// <summary>
		/// Creates the channel by calling the ChannelManager.
		/// </summary>
		/// <returns>The channel with the given name.</returns>
		/// <param name="attribute">The attribute describing the channel to create.</param>
		/// <typeparam name="T">The type of data in the channel.</typeparam>
		protected virtual IChannel<T> DoCreateChannel<T>(ChannelNameAttribute attribute)
		{
			return ChannelManager.CreateChannelForScope<T>(attribute);
		}

		/// <summary>
		/// Performs a recursive lookup to find the specified channel.
		/// Returns null if no such channel was found
		/// </summary>
		/// <returns>The channel.</returns>
		/// <param name="name">The name to look for.</param>
		internal IRetireAbleChannel RecursiveLookup(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return null;

			IRetireAbleChannel res;
			if (m_lookup.TryGetValue(name, out res))
				return res;
			
			lock (__lock)
			{
				var cur = this;
				while (cur != null)
				{
					if (cur.m_lookup.TryGetValue(name, out res))
						return res;

					if (Isolated)
						cur = null;
					else
						cur = cur.ParentScope;
				}

				return null;

			}		
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
				__scopes.Remove(this.m_instancekey);
				m_isDisposed = true;
				m_lookup = null;
			}
		}

		#endregion

#if PCL_BUILD
		private static bool __IsFirstUsage = true;
		private static ChannelScope __Current = null;

		public static ChannelScope Current
		{
			get
			{
				lock (__lock)
				{
					// TODO: Use AsyncLocal if targeting 4.6
					//var cur = new System.Threading.AsyncLocal<ChannelScope>();
					if (__IsFirstUsage)
					{
						__IsFirstUsage = false;
						System.Diagnostics.Debug.WriteLine("*Warning*: PCL does not provide a call context, so channel scoping does not work correctly for multithreaded use!");
					}

					var cur = __Current;
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
					__Current = value;
				}
			}
		}

#else
		/// <summary>
		/// Gets the current channel scope.
		/// </summary>
		/// <value>The current scope.</value>
		public static ChannelScope Current
		{
			get 
			{
				lock (__lock)
				{
					var cur = System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(LOGICAL_CONTEXT_KEY) as string;
					if (cur == null)
						return Current = Root;
					else
					{
						ChannelScope sc;
						if (!__scopes.TryGetValue(cur, out sc))
							throw new Exception(string.Format("Unable to find scope in lookup table, this may be caused by attempting to transport call contexts between AppDomains (eg. with remoting calls)"));

						return sc;
					}
				}
			}
			private set
			{
				lock (__lock)
					System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(LOGICAL_CONTEXT_KEY, value.m_instancekey);
			}
		}

#endif


	}
}

