﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;

namespace Microsoft.Framework.Caching.SqlServer
{
    internal static class SqlParameterCollectionExtensions
    {
        // For all values where the length is less than the below value, try setting the size of the
        // parameter for improved performance.
        public const int DefaultValueColumnWidth = 8000;

        public static SqlParameterCollection AddCacheItemId(this SqlParameterCollection parameters, string value)
        {
            return parameters.AddWithValue(Columns.Names.CacheItemId, SqlDbType.NVarChar, 100, value);
        }

        public static SqlParameterCollection AddCacheItemValue(this SqlParameterCollection parameters, byte[] value)
        {
            if (value != null && value.Length < DefaultValueColumnWidth)
            {
                return parameters.AddWithValue(
                    Columns.Names.CacheItemValue,
                    SqlDbType.VarBinary,
                    DefaultValueColumnWidth,
                    value);
            }
            else
            {
                // do not mention the size
                return parameters.AddWithValue(Columns.Names.CacheItemValue, SqlDbType.VarBinary, value);
            }
        }

        public static SqlParameterCollection AddExpiresAtTime(
            this SqlParameterCollection parameters,
            DateTime utcTime)
        {
            return parameters.AddWithValue(Columns.Names.ExpiresAtTimeUTC, SqlDbType.DateTime, utcTime);
        }

        public static SqlParameterCollection AddSlidingExpirationInTicks(
            this SqlParameterCollection parameters,
            TimeSpan? value)
        {
            if (value.HasValue)
            {
                return parameters.AddWithValue(
                    Columns.Names.SlidingExpirationInTicks, SqlDbType.BigInt, value.Value.Ticks);
            }
            else
            {
                return parameters.AddWithValue(Columns.Names.SlidingExpirationInTicks, SqlDbType.BigInt, DBNull.Value);
            }
        }

        public static SqlParameterCollection AddAbsoluteExpiration(
            this SqlParameterCollection parameters,
            DateTime? utcTime)
        {
            if (utcTime.HasValue)
            {
                return parameters.AddWithValue(
                    Columns.Names.AbsoluteExpirationUTC, SqlDbType.DateTime, utcTime.Value);
            }
            else
            {
                return parameters.AddWithValue(Columns.Names.AbsoluteExpirationUTC, SqlDbType.DateTime, DBNull.Value);
            }
        }

        public static SqlParameterCollection AddWithValue(
            this SqlParameterCollection parameters,
            string parameterName,
            SqlDbType dbType,
            object value)
        {
            var parameter = new SqlParameter(parameterName, dbType);
            parameter.Value = value;
            parameters.Add(parameter);
            parameter.ResetSqlDbType(); // required for mono to work
            return parameters;
        }

        public static SqlParameterCollection AddWithValue(
            this SqlParameterCollection parameters,
            string parameterName,
            SqlDbType dbType,
            int size,
            object value)
        {
            var parameter = new SqlParameter(parameterName, dbType, size);
            parameter.Value = value;
            parameters.Add(parameter);
            parameter.ResetSqlDbType(); // required for mono to work
            return parameters;
        }
    }
}