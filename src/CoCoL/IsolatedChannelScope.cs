﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace CoCoL
{
	public class IsolatedChannelScope : ChannelScope
	{
		/// <summary>
		/// Constructs a new isolated channel scope
		/// </summary>
		public IsolatedChannelScope()
			: base(true)
		{
		}

		/// <summary>
		/// Constructs a new isolated channel scope
		/// </summary>
		/// <param name="inheritedChannelNames">List of channels to inherit from the parent scope.</param>
		public IsolatedChannelScope(IEnumerable<string> inheritedChannelNames)
			: base(true)
		{
			SetupInheritedChannels(inheritedChannelNames);
		}

		/// <summary>
		/// Constructs a new isolated channel scope
		/// </summary>
		/// <param name="inheritedChannelNames">List of channels to inherit from the parent scope.</param>
		public IsolatedChannelScope(params string[] inheritedChannelNames)
			: base(true)
		{
			SetupInheritedChannels(inheritedChannelNames);
		}

		/// <summary>
		/// Constructs a new isolated channel scope
		/// </summary>
		/// <param name="inheritedChannelNames">List of channels to inherit from the parent scope.</param>
		public IsolatedChannelScope(IEnumerable<INamedItem> inheritedChannelNames)
			: base(true)
		{
			if (inheritedChannelNames != null)
				SetupInheritedChannels(from n in inheritedChannelNames where n != null select n.Name);
		}

		/// <summary>
		/// Constructs a new isolated channel scope
		/// </summary>
		/// <param name="inheritedChannelNames">List of channels to inherit from the parent scope.</param>
		public IsolatedChannelScope(params INamedItem[] inheritedChannelNames)
			: base(true)
		{
			if (inheritedChannelNames != null)
				SetupInheritedChannels(from n in inheritedChannelNames where n != null select n.Name);
		}

		/// <summary>
		/// Adds all inherited channels to the current scope,
		/// and disposes this instance if an exception is thrown
		/// </summary>
		/// <param name="names">List of channels to inherit from the parent scope.</param>
		protected void SetupInheritedChannels(IEnumerable<string> names)
		{
			try
			{
				if (names != null)
					foreach (var n in names)
						InjectChannelFromParent(n);
			}
			catch
			{
				this.Dispose();
				throw;
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

		/// <summary>
		/// Injects a channel into the current scope, by looking in the parent scope.
		/// This is particularly useful in isolated scopes, to selectively forward channels
		/// </summary>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="parent">The scope to look in, <code>null</code> means the current parent</param>
		/// <param name="channel">The channel to inject.</param>
		public void InjectChannelsFromParent(IEnumerable<string> names, ChannelScope parent = null)
		{
			foreach (var n in names)
				InjectChannelFromParent(n, parent);
		}

		/// <summary>
		/// Injects a channel into the current scope, by looking in the parent scope.
		/// This is particularly useful in isolated scopes, to selectively forward channels
		/// </summary>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="channel">The channel to inject.</param>
		public void InjectChannelsFromParent(params string[] names)
		{
			foreach (var n in names)
				InjectChannelFromParent(n);
		}

		/// <summary>
		/// Injects a channel into the current scope, by looking in the parent scope.
		/// This is particularly useful in isolated scopes, to selectively forward channels
		/// </summary>
		/// <param name="name">The name of the channel to create.</param>
		/// <param name="parent">The scope to look in, <code>null</code> means the current parent</param>
		/// <param name="channel">The channel to inject.</param>
		public void InjectChannelFromParent(string name, ChannelScope parent = null)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentNullException("name");
			parent = parent ?? this.ParentScope;

			lock (__lock)
			{
				var c = parent.RecursiveLookup(name);
				if (c == null)
					throw new Exception(string.Format("No channel with the name {0} was found in the parent scope"));

				m_lookup[name] = c;
			}
		}

	}
}
