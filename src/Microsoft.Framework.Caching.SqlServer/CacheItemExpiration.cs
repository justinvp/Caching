// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Caching.Distributed;

namespace Microsoft.Framework.Caching.SqlServer
{
    internal static class CacheItemExpiration
    {
        /// <summary>
        /// To prevent frequent updates to a cache item's expiration time in database, especially when doing a
        /// Get operation, a cache item's expiration time is extended by doubling the sliding expiration.
        /// The 'SlidingExpiration' here should be interpreted as the minimum time to live for a cache item before
        /// it expires.
        /// Example:
        /// For a sliding expiration of 30 mins, a new cache item would have its 'ExpiresAtTime' 60 mins(30 * 2)
        /// from now. All 'Get' operations before the first 30 mins do not cause any database updates to the cache
        /// item's expiration time and any 'Get' operations between the 30th and 60th minute would cause a database
        /// update where the expiration time is again extended by 60 mins.
        /// </summary>
        public const int ExpirationTimeMultiplier = 2;

        public static CacheItemExpirationInfo GetExpirationInfo(
            DateTimeOffset utcNow,
            DistributedCacheEntryOptions options)
        {
            var result = new CacheItemExpirationInfo();
            result.SlidingExpiration = options.SlidingExpiration;

            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = options.AbsoluteExpiration.Value;
            }
            result.AbsoluteExpiration = absoluteExpiration;

            result.ExpiresAtTime = GetExpirationTime(
                result.SlidingExpiration, result.AbsoluteExpiration, utcNow);

            return result;
        }

        public static DateTimeOffset? GetNewExpirationTime(
            DateTimeOffset utcNow,
            DateTimeOffset currentExpirationTime,
            TimeSpan? slidingExpiration,
            DateTimeOffset? absoluteExpiration)
        {
            // Check if the current item has a sliding expiration and is going to expire within the
            // next timeout period(ex:20 minutes), then extend the expiration time.
            if (slidingExpiration.HasValue &&
                utcNow >= (currentExpirationTime - slidingExpiration.Value))
            {
                var newExpirationTime = GetExpirationTime(
                    slidingExpiration, absoluteExpiration, utcNow);

                if (currentExpirationTime != newExpirationTime)
                {
                    return newExpirationTime;
                }
            }

            return null;
        }

        private static DateTimeOffset GetExpirationTime(
            TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration, DateTimeOffset utcNow)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs " +
                    "to be provided.");
            }

            if (slidingExpiration.HasValue)
            {
                // if there is also an absolute expiration, then the sliding expiration extension should
                // not exceed the absolute expiration.
                var newSlidingExpirationTime = utcNow + TimeSpan.FromTicks(
                    ExpirationTimeMultiplier * slidingExpiration.Value.Ticks);
                if (absoluteExpiration.HasValue && newSlidingExpirationTime > absoluteExpiration.Value)
                {
                    return absoluteExpiration.Value;
                }

                return newSlidingExpirationTime;
            }

            return absoluteExpiration.Value;
        }
    }
}
