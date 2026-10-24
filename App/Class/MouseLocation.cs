// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Threading;

using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.Class
{
    internal sealed class MouseLocation
    {
        private sealed record PendingLocation(ID Destination, Point Position);

        private PendingLocation pending;

        internal bool HasPending => Volatile.Read(ref pending) != null;

        internal void Set(ID destination, Point position)
        {
            Interlocked.Exchange(ref pending, new PendingLocation(destination, position));
        }

        internal bool TryTake(ID destination, out Point position)
        {
            PendingLocation snapshot = Volatile.Read(ref pending);
            if (snapshot != null && snapshot.Destination == destination
                && ReferenceEquals(Interlocked.CompareExchange(ref pending, null, snapshot), snapshot))
            {
                position = snapshot.Position;
                return true;
            }

            // A delayed consumer must not take the landing point of a newer switch.
            position = default;
            return false;
        }
    }
}
