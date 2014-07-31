﻿/* Copyright 2010-2013 10gen Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Threading;

namespace MongoDB.Driver.Core.Misc
{
    /// <summary>
    /// Thread-safe helper to manage a value.
    /// </summary>
    internal class InterlockedValue
    {
        // fields
        private int _value;

        // constructors
        public InterlockedValue(int initialValue)
        {
            _value = initialValue;
        }

        // properties
        public int Value
        {
            get { return Interlocked.CompareExchange(ref _value, 0, 0); }
        }

        // methods
        public bool TryChange(int newValue)
        {
            return Interlocked.Exchange(ref _value, newValue) != newValue;
        }

        public bool TryChange(int oldValue, int newValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, oldValue) != newValue;
        }
    }
}