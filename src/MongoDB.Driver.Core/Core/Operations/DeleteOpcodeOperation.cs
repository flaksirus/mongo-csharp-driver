﻿/* Copyright 2013-2014 MongoDB Inc.
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

using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.WireProtocol;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;

namespace MongoDB.Driver.Core.Operations
{
    public class DeleteOpcodeOperation : IWriteOperation<WriteConcernResult>
    {
        // fields
        private readonly CollectionNamespace _collectionNamespace;
        private readonly BsonDocument _criteria;
        private bool _isMulti;
        private readonly MessageEncoderSettings _messageEncoderSettings;
        private WriteConcern _writeConcern = WriteConcern.Acknowledged;

        // constructors
        public DeleteOpcodeOperation(
            CollectionNamespace collectionNamespace,
            BsonDocument criteria,
            MessageEncoderSettings messageEncoderSettings)
        {
            _collectionNamespace = Ensure.IsNotNull(collectionNamespace, "collectionNamespace");
            _criteria = Ensure.IsNotNull(criteria, "criteria");
            _messageEncoderSettings = messageEncoderSettings;
        }

        // properties
        public CollectionNamespace CollectionNamespace
        {
            get { return _collectionNamespace; }
        }

        public BsonDocument Criteria
        {
            get { return _criteria; }
        }

        public bool IsMulti
        {
            get { return _isMulti; }
            set { _isMulti = value; }
        }

        public MessageEncoderSettings MessageEncoderSettings
        {
            get { return _messageEncoderSettings; }
        }

        public WriteConcern WriteConcern
        {
            get { return _writeConcern; }
            set { _writeConcern = Ensure.IsNotNull(value, "value"); }
        }

        // methods
        private DeleteWireProtocol CreateProtocol()
        {
            return new DeleteWireProtocol(
                _collectionNamespace,
                _criteria,
                _isMulti,
                _messageEncoderSettings,
                _writeConcern);
        }

        public async Task<WriteConcernResult> ExecuteAsync(IConnectionHandle connection, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(connection, "connection");

            if (connection.Description.BuildInfoResult.ServerVersion >= new SemanticVersion(2, 6, 0) && _writeConcern.IsAcknowledged)
            {
                var emulator = new DeleteOpcodeOperationEmulator(_collectionNamespace, _criteria, _messageEncoderSettings)
                {
                    IsMulti = _isMulti,
                    WriteConcern = _writeConcern
                };
                return await emulator.ExecuteAsync(connection, timeout, cancellationToken);
            }
            else
            {
                var protocol = CreateProtocol();
                return await protocol.ExecuteAsync(connection, timeout, cancellationToken);
            }
        }

        public async Task<WriteConcernResult> ExecuteAsync(IWriteBinding binding, TimeSpan timeout = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(binding, "binding");
            var slidingTimeout = new SlidingTimeout(timeout);
            using (var connectionSource = await binding.GetWriteConnectionSourceAsync(slidingTimeout, cancellationToken))
            using (var connection = await connectionSource.GetConnectionAsync(slidingTimeout, cancellationToken))
            {
                return await ExecuteAsync(connection, slidingTimeout, cancellationToken);
            }
        }
    }
}