﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Agent.Extensions.Providers;

namespace NewRelic.Providers.Storage.AsyncProviders
{
    /// <summary>
    /// Multiple instances of this class will share state per type T because we use a
    /// static AsyncLocal instance.  AsyncLocal doesn't behave very well unless it's defined
    /// as a static variable.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncTransactionContext<T> : IContextStorage<T>
    {
        private static readonly AsyncLocal<T> _context = new AsyncLocal<T>();

        public byte Priority => 2;
        public bool CanProvide => true;

        public T GetData()
        {
            return _context.Value;
        }

        public void SetData(T value)
        {
            _context.Value = value;
        }

        public void Clear()
        {
            _context.Value = default;
        }
    }
}
