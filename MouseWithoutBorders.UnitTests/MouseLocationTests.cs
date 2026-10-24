// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class MouseLocationTests
{
    private const ID Local = (ID)1;
    private const ID Remote = (ID)2;

    [TestMethod]
    public void LandingPointIsConsumedOnlyOnce()
    {
        var location = new MouseLocation();
        Assert.IsFalse(location.HasPending);
        Assert.IsFalse(location.TryTake(Local, out _));

        // (0, 0) is a valid landing point, not an empty request.
        location.Set(Local, Point.Empty);
        Assert.IsTrue(location.HasPending);
        Assert.IsTrue(location.TryTake(Local, out var position));
        Assert.AreEqual(Point.Empty, position);
        Assert.IsFalse(location.HasPending);
        Assert.IsFalse(location.TryTake(Local, out _));
    }

    [TestMethod]
    public async Task DelayedLocalHelperDoesNotConsumeNextRemoteLandingPoint()
    {
        var location = new MouseLocation();
        location.Set(Local, new Point(64000, 14000));
        using var helperReady = new ManualResetEventSlim();
        using var resumeHelper = new ManualResetEventSlim();

        // The helper has already selected the local branch when the next switch arrives.
        var helper = Task.Run(() =>
        {
            helperReady.Set();
            if (!resumeHelper.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Helper was not resumed.");
            }

            return location.TryTake(Local, out _);
        });

        try
        {
            Assert.IsTrue(helperReady.Wait(TimeSpan.FromSeconds(10)));
            location.Set(Remote, new Point(70, 42000));
        }
        finally
        {
            resumeHelper.Set();
        }

        Assert.IsFalse(await helper.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.IsTrue(location.HasPending);
        Assert.IsTrue(location.TryTake(Remote, out var remotePosition));
        Assert.AreEqual(new Point(70, 42000), remotePosition);
    }

    [TestMethod]
    public void DelayedRemoteHookDoesNotConsumeReturnToLocalLandingPoint()
    {
        var location = new MouseLocation();
        location.Set(Remote, new Point(70, 42000));
        location.Set(Local, new Point(64000, 14000));

        Assert.IsFalse(location.TryTake(Remote, out _));
        Assert.IsTrue(location.TryTake(Local, out var localPosition));
        Assert.AreEqual(new Point(64000, 14000), localPosition);
    }

    [TestMethod]
    public void RepeatedSwitchesPreserveDestinationAndBothCoordinates()
    {
        var location = new MouseLocation();
        for (int i = 0; i < 1000; i++)
        {
            ID destination = i % 2 == 0 ? Local : Remote;
            ID previousDestination = destination == Local ? Remote : Local;
            var expected = new Point(i * 61, 65535 - (i * 47));
            location.Set(previousDestination, new Point(300001, 300002));
            location.Set(destination, expected);

            Assert.IsFalse(location.TryTake(previousDestination, out _));
            Assert.IsTrue(location.TryTake(destination, out var actual));
            Assert.AreEqual(expected, actual);
            Assert.IsFalse(location.TryTake(destination, out _));
        }
    }

    [TestMethod]
    public void ConcurrentConsumersCanClaimLandingPointOnlyOnce()
    {
        var location = new MouseLocation();
        for (int i = 0; i < 100; i++)
        {
            var expected = new Point(i, 65535 - i);
            location.Set(Local, expected);
            int claims = 0;
            Parallel.For(0, 16, _ =>
            {
                if (location.TryTake(Local, out var actual))
                {
                    Assert.AreEqual(expected, actual);
                    Interlocked.Increment(ref claims);
                }
            });

            Assert.AreEqual(1, claims);
            Assert.IsFalse(location.HasPending);
        }
    }
}
