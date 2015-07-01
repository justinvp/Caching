// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace SqlServerCache
{
    internal class SqlQueries
    {
        private const string CreateTableFormat = "CREATE TABLE {0}(" +
            // add collation to the key column to make it case-sensitive
            "Id nvarchar(900) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL, " +
            "Value varbinary(MAX) NOT NULL, " +
            "ExpiresAtTime datetimeoffset NOT NULL, " +
            "SlidingExpirationInTicks bigint NULL," +
            "AbsoluteExpiration datetimeoffset NULL, " +
            "CONSTRAINT pk_Id PRIMARY KEY (Id))";

        private const string CreateNonClusteredIndexOnExpirationTimeFormat
            = "CREATE NONCLUSTERED INDEX Index_ExpiresAtTime ON {0}(ExpiresAtTime)";

        private const string TableInfoFormat =
             "SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE " +
             "FROM INFORMATION_SCHEMA.TABLES " +
             "WHERE TABLE_SCHEMA = '{0}' " +
             "AND TABLE_NAME = '{1}'";

        public SqlQueries(string schemaName, string tableName)
        {
            //TODO: sanitize schema and table name

            var tableNameWithSchema = string.Format("[{0}].[{1}]", schemaName, tableName);
            CreateTable = string.Format(CreateTableFormat, tableNameWithSchema);
            CreateNonClusteredIndexOnExpirationTime = string.Format(
                CreateNonClusteredIndexOnExpirationTimeFormat,
                tableNameWithSchema);
            TableInfo = string.Format(TableInfoFormat, schemaName, tableName);
        }

        public string CreateTable { get; }

        public string CreateNonClusteredIndexOnExpirationTime { get; }

        public string TableInfo { get; }
    }
}
