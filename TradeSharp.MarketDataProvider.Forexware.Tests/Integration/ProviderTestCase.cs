﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TradeHub.Common.Core.DomainModels;
using TradeHub.Common.Core.ValueObjects.MarketData;
using TradeSharp.MarketDataProvider.Forexware.Provider;

namespace TradeSharp.MarketDataProvider.Forexware.Tests.Integration
{
    [TestFixture]
    public class ProviderTestCase
    {
        private ForexwareMarketDataProvider _marketDataProvider;
        [SetUp]
        public void SetUp()
        {
            _marketDataProvider = new ForexwareMarketDataProvider();
        }

        [Test]
        [Category("Integration")]
        public void MarketDataProviderReceiveLogon()
        {
            bool isConnected = false;
            var manualLogonEvent = new ManualResetEvent(false);

            _marketDataProvider.LogonArrived +=
                    delegate (string obj)
                    {
                        isConnected = true;
                        //_marketDataProvider.Stop();
                        manualLogonEvent.Set();
                    };

            _marketDataProvider.Start();
            manualLogonEvent.WaitOne(30000, false);

            Assert.AreEqual(true, isConnected);
        }

        [Test]
        [Category("Integration")]
        public void MarketDataProviderReceiveLogout()
        {
            bool isConnected = false;
            var manualLogonEvent = new ManualResetEvent(false);
            _marketDataProvider.LogonArrived +=
                    delegate (string obj)
                    {
                        isConnected = true;
                        _marketDataProvider.Stop();
                        manualLogonEvent.Set();
                    };

            bool isDisconnected = false;
            var manualLogoutEvent = new ManualResetEvent(false);
            _marketDataProvider.LogoutArrived +=
                    delegate (string obj)
                    {
                        isDisconnected = true;
                        manualLogoutEvent.Set();
                    };

            _marketDataProvider.Start();
            manualLogonEvent.WaitOne(30000, false);
            manualLogoutEvent.WaitOne(30000, false);

            Assert.AreEqual(true, isConnected, "Connected");
            Assert.AreEqual(true, isDisconnected, "Disconnected");
        }

        [Test]
        [Category("Integration")]
        public void SubscribeData_SendRequestToFixServer_ReceiveData()
        {
            bool isConnected = false;
            bool tickArrived = false;

            var manualLogonEvent = new ManualResetEvent(false);
            var manualTickEvent = new ManualResetEvent(false);

            _marketDataProvider.LogonArrived +=
                    delegate (string obj)
                    {
                        isConnected = true;
                        _marketDataProvider.SubscribeTickData(new Subscribe() {Id = "MkRqID", Security = new Security() { Symbol = "EUR/USD" } });
                        manualLogonEvent.Set();
                    };

            _marketDataProvider.TickArrived +=
                    delegate (Tick obj)
                    {
                        tickArrived = true;
                        _marketDataProvider.Stop();
                        manualTickEvent.Set();
                    };

            _marketDataProvider.Start();
            manualLogonEvent.WaitOne(30000, false);
            manualTickEvent.WaitOne(30000, false);
            Assert.AreEqual(true, isConnected, "Is Market Data Provider connected");
            Assert.AreEqual(true, tickArrived, "Tick arrived");
        }
    }
}
