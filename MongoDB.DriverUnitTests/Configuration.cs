﻿/* Copyright 2010-2014 MongoDB Inc.
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
using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoDB.DriverUnitTests
{
    /// <summary>
    /// A static class to handle online test configuration.
    /// </summary>
    public static class Configuration
    {
        // private static fields
        private static MongoClient __testClient;
        private static MongoServer __testServer;
        private static MongoDatabase __testDatabase;
        private static MongoCollection<BsonDocument> __testCollection;
        private static bool __testServerIsReplicaSet;

        // static constructor
        static Configuration()
        {
            var connectionString = Environment.GetEnvironmentVariable("CSharpDriverTestsConnectionString")
                ?? "mongodb://localhost/?w=1"; 

            var mongoUrl = new MongoUrl(connectionString);
            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
            if (!clientSettings.WriteConcern.IsAcknowledged)
            {
                clientSettings.WriteConcern.W = 1; // ensure WriteConcern is acknowledged regardless of what the URL says
            }

            __testClient = new MongoClient(clientSettings);
            __testServer = __testClient.GetServer();
            __testDatabase = __testServer.GetDatabase(mongoUrl.DatabaseName ?? "csharpdriverunittests");
            __testCollection = __testDatabase.GetCollection("testcollection");

            // connect early so BuildInfo will be populated
            __testServer.Connect();

            var isMasterResult = __testDatabase.RunCommand("isMaster");
            __testServerIsReplicaSet = isMasterResult.Response.Contains("setName");
        }

        // public static properties
        /// <summary>
        /// Gets the test client.
        /// </summary>
        public static MongoClient TestClient
        {
            get { return __testClient; }
        }

        /// <summary>
        /// Gets the test collection.
        /// </summary>
        public static MongoCollection<BsonDocument> TestCollection
        {
            get { return __testCollection; }
        }

        /// <summary>
        /// Gets the test database.
        /// </summary>
        public static MongoDatabase TestDatabase
        {
            get { return __testDatabase; }
        }

        /// <summary>
        /// Gets the test server.
        /// </summary>
        public static MongoServer TestServer
        {
            get { return __testServer; }
        }

        /// <summary>
        /// Gets whether the tage MongoDB is a replica set.
        /// </summary>
        public static bool TestServerIsReplicaSet
        {
            get { return __testServerIsReplicaSet; }
        }

        // public static methods
        /// <summary>
        /// Gets the storage engine.
        /// </summary>
        /// <returns>The storage engine.</returns>
        public static string GetStorageEngine()
        {
            var adminDatabase = __testServer.GetDatabase("admin");
            var response = adminDatabase.RunCommand("serverStatus").Response;
            BsonValue storageEngine;
            if (response.TryGetValue("storageEngine", out storageEngine) && storageEngine.AsBsonDocument.Contains("name"))
            {
                return storageEngine["name"].AsString;
            }
            else
            {
                return "mmapv1";
            }
        }

        /// <summary>
        /// Gets the test collection with a default document type of T.
        /// </summary>
        /// <typeparam name="T">The default document type.</typeparam>
        /// <returns>The collection.</returns>
        public static MongoCollection<T> GetTestCollection<T>()
        {
            return __testDatabase.GetCollection<T>(__testCollection.Name);
        }

        public static void StartReplication(MongoServerInstance secondary)
        {
            using (__testServer.RequestStart(null, secondary))
            {
                var adminDatabaseSettings = new MongoDatabaseSettings { ReadPreference = ReadPreference.Secondary };
                var adminDatabase = __testServer.GetDatabase("admin", adminDatabaseSettings);
                var command = new CommandDocument
                {
                    { "configureFailPoint", "rsSyncApplyStop"},
                    { "mode", "off" }
                };
                adminDatabase.RunCommand(command);
            }
        }

        public static IDisposable StopReplication(MongoServerInstance secondary)
        {
            using (__testServer.RequestStart(null, secondary))
            {
                var adminDatabaseSettings = new MongoDatabaseSettings { ReadPreference = ReadPreference.Secondary };
                var adminDatabase = __testServer.GetDatabase("admin", adminDatabaseSettings);
                var command = new CommandDocument
                {
                    { "configureFailPoint", "rsSyncApplyStop"},
                    { "mode", "alwaysOn" }
                };
                adminDatabase.RunCommand(command);

                return new ReplicationRestarter(secondary);
            }
        }

        // nested types
        private class ReplicationRestarter : IDisposable
        {
            MongoServerInstance _secondary;

            public ReplicationRestarter(MongoServerInstance secondary)
            {
                _secondary = secondary;
            }

            public void Dispose()
            {
                Configuration.StartReplication(_secondary);
            }
        }
    }
}
