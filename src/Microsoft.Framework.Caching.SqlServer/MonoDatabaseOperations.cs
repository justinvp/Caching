// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Internal;

namespace Microsoft.Framework.Caching.SqlServer
{
    internal class MonoDatabaseOperations : DatabaseOperations
    {
        public MonoDatabaseOperations(
            string connectionString, string schemaName, string tableName, ISystemClock systemClock)
            : base(connectionString, schemaName, tableName, systemClock)
        {
        }

        protected override byte[] GetCacheItem(string key, bool includeValue)
        {
            var utcNow = SystemClock.UtcNow;

            string query;
            if (includeValue)
            {
                query = SqlQueries.GetCacheItem;
            }
            else
            {
                query = SqlQueries.GetCacheItemExpirationInfo;
            }

            byte[] value = null;
            TimeSpan? slidingExpiration = null;
            DateTimeOffset? absoluteExpiration = null;
            DateTimeOffset expirationTime;
            using (var connection = new SqlConnection(ConnectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddWithValue("UtcNow", SqlDbType.DateTime, utcNow.UtcDateTime);

                connection.Open();

                var reader = command.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SingleResult);

                if (reader.Read())
                {
                    var id = reader.GetString(Columns.Indexes.CacheItemIdIndex);

                    expirationTime = DateTimeOffset.Parse(reader[Columns.Indexes.ExpiresAtTimeIndex].ToString());

                    if (!reader.IsDBNull(Columns.Indexes.SlidingExpirationInTicksIndex))
                    {
                        slidingExpiration = TimeSpan.FromTicks(
                            reader.GetInt64(Columns.Indexes.SlidingExpirationInTicksIndex));
                    }

                    if (!reader.IsDBNull(Columns.Indexes.AbsoluteExpirationIndex))
                    {
                        absoluteExpiration = DateTimeOffset.Parse(
                            reader[Columns.Indexes.AbsoluteExpirationIndex].ToString());
                    }

                    if (includeValue)
                    {
                        value = (byte[])reader[Columns.Indexes.CacheItemValueIndex];
                    }
                }
                else
                {
                    return null;
                }
            }

            UpdateCacheItemExpiration(
                key, utcNow, expirationTime, slidingExpiration, absoluteExpiration);

            return value;
        }

        protected override async Task<byte[]> GetCacheItemAsync(string key, bool includeValue)
        {
            var utcNow = SystemClock.UtcNow;

            string query;
            if (includeValue)
            {
                query = SqlQueries.GetCacheItem;
            }
            else
            {
                query = SqlQueries.GetCacheItemExpirationInfo;
            }

            byte[] value = null;
            TimeSpan? slidingExpiration = null;
            DateTimeOffset? absoluteExpiration = null;
            DateTimeOffset expirationTime;
            using (var connection = new SqlConnection(ConnectionString))
            {
                var command = new SqlCommand(SqlQueries.GetCacheItem, connection);
                command.Parameters
                    .AddCacheItemId(key)
                    .AddWithValue("UtcNow", SqlDbType.DateTime, utcNow.UtcDateTime);

                await connection.OpenAsync();

                var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SingleRow | CommandBehavior.SingleResult);

                if (await reader.ReadAsync())
                {
                    var id = reader.GetString(Columns.Indexes.CacheItemIdIndex);

                    expirationTime = DateTimeOffset.Parse(reader[Columns.Indexes.ExpiresAtTimeIndex].ToString());

                    if (!await reader.IsDBNullAsync(Columns.Indexes.SlidingExpirationInTicksIndex))
                    {
                        slidingExpiration = TimeSpan.FromTicks(
                            Convert.ToInt64(reader[Columns.Indexes.SlidingExpirationInTicksIndex].ToString()));
                    }

                    if (!await reader.IsDBNullAsync(Columns.Indexes.AbsoluteExpirationIndex))
                    {
                        absoluteExpiration = DateTimeOffset.Parse(
                            reader[Columns.Indexes.AbsoluteExpirationIndex].ToString());
                    }

                    if (includeValue)
                    {
                        value = (byte[])reader[Columns.Indexes.CacheItemValueIndex];
                    }
                }
                else
                {
                    return null;
                }
            }

            await UpdateCacheItemExpirationAsync(
                key, utcNow, expirationTime, slidingExpiration, absoluteExpiration);

            return value;
        }

        public override void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var utcNow = SystemClock.UtcNow;

            var expirationInfo = CacheItemExpiration.GetExpirationInfo(utcNow, options);

            using (var connection = new SqlConnection(ConnectionString))
            {
                var upsertCommand = new SqlCommand(SqlQueries.SetCacheItem, connection);
                upsertCommand.Parameters
                    .AddCacheItemId(key)
                    .AddCacheItemValue(value)
                    .AddExpiresAtTimeMono(expirationInfo.ExpiresAtTime)
                    .AddSlidingExpirationInTicks(expirationInfo.SlidingExpiration)
                    .AddAbsoluteExpirationMono(expirationInfo.AbsoluteExpiration);

                connection.Open();

                try
                {
                    upsertCommand.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    if (IsDuplicateKeyException(ex))
                    {
                        // There is a possibility that multiple requests can try to add the same item to the cache, in
                        // which case we receive a 'duplicate key' exception on the primary key column.
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
        }

        public override async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var utcNow = SystemClock.UtcNow;

            var expirationInfo = CacheItemExpiration.GetExpirationInfo(utcNow, options);

            using (var connection = new SqlConnection(ConnectionString))
            {
                var upsertCommand = new SqlCommand(SqlQueries.SetCacheItem, connection);
                upsertCommand.Parameters
                    .AddCacheItemId(key)
                    .AddCacheItemValue(value)
                    .AddExpiresAtTimeMono(expirationInfo.ExpiresAtTime)
                    .AddSlidingExpirationInTicks(expirationInfo.SlidingExpiration)
                    .AddAbsoluteExpirationMono(expirationInfo.AbsoluteExpiration);

                await connection.OpenAsync();

                try
                {
                    await upsertCommand.ExecuteNonQueryAsync();
                }
                catch (SqlException ex)
                {
                    if (IsDuplicateKeyException(ex))
                    {
                        // There is a possibility that multiple requests can try to add the same item to the cache, in
                        // which case we receive a 'duplicate key' exception on the primary key column.
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
        }

        public override void DeleteExpiredCacheItems()
        {
            var utcNow = SystemClock.UtcNow;

            using (var connection = new SqlConnection(ConnectionString))
            {
                var command = new SqlCommand(SqlQueries.DeleteExpiredCacheItems, connection);
                command.Parameters.AddWithValue("UtcNow", SqlDbType.DateTime, utcNow.UtcDateTime);

                connection.Open();

                var effectedRowCount = command.ExecuteNonQuery();
            }
        }

        protected override void UpdateCacheItemExpiration(
            string key,
            DateTimeOffset utcNow,
            DateTimeOffset currentExpirationTime,
            TimeSpan? slidingExpiration,
            DateTimeOffset? absoluteExpiration)
        {
            var newExpirationTime = CacheItemExpiration.GetNewExpirationTime(
                    utcNow, currentExpirationTime, slidingExpiration, absoluteExpiration);

            if (newExpirationTime.HasValue)
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    var command = new SqlCommand(SqlQueries.UpdateCacheItemExpiration, connection);
                    command.Parameters
                        .AddCacheItemId(key)
                        .AddExpiresAtTimeMono(newExpirationTime.Value);

                    command.ExecuteNonQuery();
                }
            }
        }

        protected override async Task UpdateCacheItemExpirationAsync(
            string key,
            DateTimeOffset utcNow,
            DateTimeOffset currentExpirationTime,
            TimeSpan? slidingExpiration,
            DateTimeOffset? absoluteExpiration)
        {
            var newExpirationTime = CacheItemExpiration.GetNewExpirationTime(
                    utcNow, currentExpirationTime, slidingExpiration, absoluteExpiration);

            if (newExpirationTime.HasValue)
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    var command = new SqlCommand(SqlQueries.UpdateCacheItemExpiration, connection);
                    command.Parameters
                        .AddCacheItemId(key)
                        .AddExpiresAtTimeMono(newExpirationTime.Value);

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}