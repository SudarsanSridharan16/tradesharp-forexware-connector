/***************************************************************************** 
* Copyright 2016 Aurora Solutions 
* 
*    http://www.aurorasolutions.io 
* 
* Aurora Solutions is an innovative services and product company at 
* the forefront of the software industry, with processes and practices 
* involving Domain Driven Design(DDD), Agile methodologies to build 
* scalable, secure, reliable and high performance products.
* 
* TradeSharp is a C# based data feed and broker neutral Algorithmic 
* Trading Platform that lets trading firms or individuals automate 
* any rules based trading strategies in stocks, forex and ETFs. 
* TradeSharp allows users to connect to providers like Tradier Brokerage, 
* IQFeed, FXCM, Blackwood, Forexware, Integral, HotSpot, Currenex, 
* Interactive Brokers and more. 
* Key features: Place and Manage Orders, Risk Management, 
* Generate Customized Reports etc 
* 
* Licensed under the Apache License, Version 2.0 (the "License"); 
* you may not use this file except in compliance with the License. 
* You may obtain a copy of the License at 
* 
*    http://www.apache.org/licenses/LICENSE-2.0 
* 
* Unless required by applicable law or agreed to in writing, software 
* distributed under the License is distributed on an "AS IS" BASIS, 
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
* See the License for the specific language governing permissions and 
* limitations under the License. 
*****************************************************************************/


﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickFix;
using TraceSourceLogger;
using TradeHub.Common.Core.DomainModels;
using TradeHub.Common.Core.DomainModels.OrderDomain;
using TradeHub.Common.Core.MarketDataProvider;
using TradeHub.Common.Core.ValueObjects.MarketData;
using TradeHub.Common.Fix.Constants;
using TradeHub.Common.Fix.Infrastructure;
using Message = QuickFix.Message;

namespace TradeSharp.MarketDataProvider.Forexware.Provider
{
    public class ForexwareMarketDataProvider : MessageCracker, IApplication, ILiveTickDataProvider
    {
        private Type _type = typeof(ForexwareMarketDataProvider);

        private string _provider = TradeHub.Common.Core.Constants.MarketDataProvider.Forexware;

        private bool _isConnected = false;

        #region Local Events

        public event Action<string> LogonArrived;
        public event Action<string> LogoutArrived;
        public event Action<Tick> TickArrived;
        public event Action<MarketDataEvent> MarketDataRejectionArrived;

        #endregion

        #region ForexwareFIXConnector Members

        private string _fixSettingsFile = string.Empty;
        private string _quoteSenderCompId = string.Empty;
        private string _quoteTargetCompId = string.Empty;
        private string _senderSubId = string.Empty;
        private string _deliverToCompId = string.Empty;
        private string _heartBeatInt = string.Empty;

        private QuickFix.IInitiator _initiator = null;
        private QuickFix.SessionID _quoteSessionId = null;

        #endregion

        /// <summary>
        /// Default Constructor
        /// </summary>
        public ForexwareMarketDataProvider()
        {
            _fixSettingsFile = AppDomain.CurrentDomain.BaseDirectory + @"\Config\ForexwareFIXSettings.txt";
        }

        /// <summary>
        /// Is Market Data client connected
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            return (_quoteSessionId != null && Session.LookupSession(_quoteSessionId).IsLoggedOn);
        }

        /// <summary>
        /// Connects/Starts a client
        /// </summary>
        public bool Start()
        {
            try
            {
                if (this._initiator == null)
                {
                    PopulateFixSettings();

                    QuickFix.SessionSettings settings = new QuickFix.SessionSettings(this._fixSettingsFile);
                    IApplication application = this;
                    QuickFix.FileStoreFactory storeFactory = new QuickFix.FileStoreFactory(settings);
                    QuickFix.FileLogFactory logFactory = new QuickFix.FileLogFactory(settings);
                    QuickFix.IMessageStoreFactory messageFactory = new QuickFix.FileStoreFactory(settings);

                    this._initiator = new QuickFix.Transport.SocketInitiator(application, storeFactory, settings, logFactory);

                    this._initiator.Start();

                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info("Forexware Data Client Started.", _type.FullName, "Start");
                    }
                }
                else
                {
                    if (!this._initiator.IsStopped)
                    {
                        if (Logger.IsInfoEnabled)
                        {
                            Logger.Info("Forexware Data Client Already Started.", _type.FullName, "Start");
                        }
                    }
                    else
                    {
                        this._initiator.Start();
                        if (Logger.IsInfoEnabled)
                        {
                            Logger.Info("Forexware Data Client Started.", _type.FullName, "Start");
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "Start");
            }
            return false;
        }

        /// <summary>
        /// Disconnects/Stops a client
        /// </summary>
        public bool Stop()
        {
            try
            {
                if (!this._initiator.IsStopped)
                {
                    this._initiator.Stop();
                    this._initiator.Dispose();
                    this._initiator = null;

                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info("Forexware Data Client Stoped.", _type.FullName, "Stop");
                    }
                }
                else
                {
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info("Forexware Data Client Already Stoped.", _type.FullName, "Stop");
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "Stop");
            }
            return false;
        }

        /// <summary>
        /// Market data request message
        /// </summary>
        public bool SubscribeTickData(Subscribe request)
        {
            try
            {
                if (IsConnected())
                {
                    Session.SendToTarget(
                        MarketDataRequest(request.Id, request.Security, SubscriptionType.SubscribeSnapshotUpdate, 1),
                        this._quoteSessionId);
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Successful - " + request, _type.FullName, "SubscribeTickData");
                    }
                    return true;
                }
                else
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Failed - " + request, _type.FullName, "SubscribeTickData");
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "SubscribeTickData");
            }
            return false;
        }

        /// <summary>
        /// Unsubscribe Market data message
        /// </summary>
        public bool UnsubscribeTickData(Unsubscribe request)
        {
            try
            {
                if (IsConnected())
                {
                    Session.SendToTarget(
                        MarketDataRequest(request.Id, request.Security, SubscriptionType.UnsubscribeSnapshotUpdate, 1),
                        this._quoteSessionId);
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Successful - " + request, _type.FullName, "UnsubscribeTickData");
                    }
                    return true;
                }
                else
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Failed - " + request, _type.FullName, "UnsubscribeTickData");
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "UnsubscribeTickData");
            }
            return false;
        }

        #region Application Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        public void FromAdmin(Message message, SessionID sessionId)
        {
            Crack(message, sessionId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        public void FromApp(Message message, SessionID sessionId)
        {
            Crack(message, sessionId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sessionId"></param>
        public void OnCreate(SessionID sessionId)
        {
            //Do nothing
        }

        /// <summary>
        /// Quick fix on logon method.
        /// This callback notifies you when a valid logon has been established with a counter party. This is called when 
        /// a connection has been established and the FIX logon process has completed with both parties exchanging valid 
        /// logon messages. 
        /// </summary>
        /// <param name="sessionId"></param>
        public void OnLogon(SessionID sessionId)
        {
            try
            {
                this._isConnected = true;

                if (this._quoteSenderCompId.Equals(sessionId.SenderCompID))
                {
                    this._quoteSessionId = sessionId;
                }

                if (LogonArrived != null)
                {
                    LogonArrived(_provider);
                }

                if (Logger.IsInfoEnabled)
                {
                    Logger.Info(
                        " FIX OnLogon - SenderCompID : " + sessionId.SenderCompID + " -> TargetCompID : " +
                        sessionId.TargetCompID, _type.FullName, "OnLogon");
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnLogon");
            }
        }

        /// <summary>
        /// Quick fix on logout method.
        /// This callback notifies you when an FIX session is no longer online. This could happen during a normal logout 
        /// exchange or because of a forced termination or a loss of network connection. 
        /// </summary>
        /// <param name="sessionId"></param>
        public void OnLogout(SessionID sessionId)
        {
            try
            {
                this._isConnected = false;

                if (this._quoteSenderCompId.Equals(sessionId.SenderCompID))
                {
                    this._quoteSessionId = null;
                }

                if (LogoutArrived != null)
                {
                    LogoutArrived(_provider);
                }

                if (Logger.IsInfoEnabled)
                {
                    Logger.Info(
                        " FIX OnLogout - SenderCompID : " + sessionId.SenderCompID + " -> TargetCompID : " +
                        sessionId.TargetCompID, _type.FullName, "OnLogout");
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnLogout");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void ToAdmin(Message message, SessionID sessionID)
        {
            Crack(message, sessionID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionID"></param>
        public void ToApp(Message message, SessionID sessionID)
        {
            Crack(message, sessionID);
        }
        #endregion

        #region Message Crackers

        /// <summary>
        /// FIX43 message cracker
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        public void Crack(Message message, SessionID sessionId)
        {
            if (message is QuickFix.FIX44.Reject)
            {
                OnMessage((QuickFix.FIX44.Reject)(message), sessionId);
            }
            else if (message is QuickFix.FIX44.MarketDataSnapshotFullRefresh)
            {
                OnMessage((QuickFix.FIX44.MarketDataSnapshotFullRefresh)(message), sessionId);
            }
            else if (message is QuickFix.FIX44.Logon)
            {
                OnMessage((QuickFix.FIX44.Logon)(message), sessionId);
            }
            else if (message is QuickFix.FIX44.MarketDataRequestReject)
            {
                OnMessage((QuickFix.FIX44.MarketDataRequestReject)(message), sessionId);
            }

        }

        #region Logon Handler
        /// <summary>
        /// Set parameters before sending the message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sessionId"></param>
        private void OnMessage(QuickFix.FIX44.Logon message, SessionID sessionId)
        {
            try
            {
                QuickFix.Fields.ResetSeqNumFlag resetSeqNumFlag = new QuickFix.Fields.ResetSeqNumFlag(true);
                QuickFix.Fields.EncryptMethod encryptMethod = new QuickFix.Fields.EncryptMethod(0);
                QuickFix.Fields.HeartBtInt heartBtInt = new QuickFix.Fields.HeartBtInt(GetHeartBeatInterval());

                // Set values in the message body before sending
                message.ResetSeqNumFlag = resetSeqNumFlag;
                message.EncryptMethod = encryptMethod;
                message.HeartBtInt = heartBtInt;
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnMessage");
            }
        }
        #endregion

        #region Market Data Handler
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message">Market data snapshot full refresh message</param>
        /// <param name="session">Session ID</param>
        private void OnMessage(QuickFix.FIX44.MarketDataSnapshotFullRefresh message, QuickFix.SessionID session)
        {
            try
            {
                int numberOfMarketDataEntries = message.NoMDEntries.getValue();

                for (int i = 1; i <= numberOfMarketDataEntries; i++)
                {
                    Tick tick = new Tick(new Security() { Symbol = message.Symbol.getValue() }, _provider);

                    tick.DateTime = message.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                    QuickFix.Group group = message.GetGroup(i, QuickFix.Fields.Tags.NoMDEntries);
                    if (group.GetField(QuickFix.Fields.Tags.MDEntryType).Equals("0"))
                    {
                        tick.BidPrice = Convert.ToDecimal(group.GetField(QuickFix.Fields.Tags.MDEntryPx));
                        tick.BidSize = Convert.ToDecimal(group.GetField(QuickFix.Fields.Tags.MDEntrySize));
                    }
                    if (group.GetField(QuickFix.Fields.Tags.MDEntryType).Equals("1"))
                    {
                        tick.AskPrice = Convert.ToDecimal(group.GetField(QuickFix.Fields.Tags.MDEntryPx));
                        tick.AskSize = Convert.ToDecimal(group.GetField(QuickFix.Fields.Tags.MDEntrySize));
                    }

                    if (TickArrived != null)
                    {
                        TickArrived(tick);
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnMessage");
            }
        }
        #endregion

        #region Market Data Request Reject

        /// <summary>
        /// Handles market data request reject
        /// </summary>
        /// <param name="reject"></param>
        /// <param name="sessionId"></param>
        private void OnMessage(QuickFix.FIX44.MarketDataRequestReject reject, SessionID sessionId)
        {
            try
            {
                //MarketDataReject marketDataReject = new MarketDataReject(reject.MDReqID.getValue(), reject.MDReqRejReason.getValue(), reject.Text.getValue());
                MarketDataEvent marketDataReject = new MarketDataEvent(new Security(), _provider);

                if (MarketDataRejectionArrived != null)
                {
                    MarketDataRejectionArrived(marketDataReject);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnMessage");
            }
        }

        #endregion

        #region Business Reject Handler

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reject"></param>
        /// <param name="sessionId"></param>
        private void OnMessage(QuickFix.FIX44.Reject reject, SessionID sessionId)
        {
            try
            {
                // SessionRejectReason (373)
                // 0 = Invalid tag number
                // 1 = Required tag missing
                // 2 = Tag not defined for this message type
                // 3 = Undefined Tag
                // 4 = Tag specified without a value
                // 5 = Value is incorrect (out of range) for this tag
                // 6 = Incorrect data format for value
                // 7 = Decryption problem
                // 8 = Signature problem
                // 9 = CompID problem
                // 10 = SendingTime (52) accuracy problem
                // 11 = Invalid MsgType (35)
                // (Note other session-level rule violations may exist in which case SessionRejectReason (373) is not specified) 

                if (Logger.IsInfoEnabled)
                {
                    Logger.Info(
                        "Message rejected at business level : " +
                        reject.GetField(58).ToString(CultureInfo.InvariantCulture),
                        _type.FullName, "OnMessage");
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString(), _type.FullName, "OnMessage");
            }
        }

        #endregion

        #endregion

        #region Message Creators

        #region Create Market Data Request Message

        /// <summary>
        /// Creates a FIX4.4 MarketDataRequest message.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="security"></param>
        /// <param name="subscriptionType"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public QuickFix.FIX44.MarketDataRequest MarketDataRequest(string id, Security security, char subscriptionType, int depth)
        {
            QuickFix.FIX44.MarketDataRequest marketDataRequest = new QuickFix.FIX44.MarketDataRequest();

            QuickFix.Fields.NoRelatedSym noRelatedSym = new QuickFix.Fields.NoRelatedSym(1);
            marketDataRequest.SetField(noRelatedSym);

            QuickFix.Fields.MDReqID mdReqId = new QuickFix.Fields.MDReqID(id);
            marketDataRequest.SetField(mdReqId);

            QuickFix.Fields.SubscriptionRequestType subscriptionRequestType = new QuickFix.Fields.SubscriptionRequestType(subscriptionType);
            marketDataRequest.SetField(subscriptionRequestType);

            QuickFix.Fields.MarketDepth marketDepth = new QuickFix.Fields.MarketDepth(depth);
            marketDataRequest.SetField(marketDepth);

            QuickFix.Fields.MDUpdateType mdUpdateType = new QuickFix.Fields.MDUpdateType(MarketDataUpdateType.FullRefresh);
            marketDataRequest.SetField(mdUpdateType);

            QuickFix.Fields.NoMDEntryTypes noMdEntryType = new QuickFix.Fields.NoMDEntryTypes(2);
            marketDataRequest.SetField(noMdEntryType);

            QuickFix.Fields.Symbol symbol = new QuickFix.Fields.Symbol(security.Symbol);
            QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup relatedSymbols = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            relatedSymbols.SetField(symbol);
            marketDataRequest.AddGroup(relatedSymbols);

            QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup mdEntryTypes = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            {
                mdEntryTypes.SetField(new QuickFix.Fields.MDEntryType(MarketDataEntryType.Bid));
                marketDataRequest.AddGroup(mdEntryTypes);
                mdEntryTypes.SetField(new QuickFix.Fields.MDEntryType(MarketDataEntryType.Offer));
                marketDataRequest.AddGroup(mdEntryTypes);
            }

            return marketDataRequest;
        }
        #endregion

        #endregion

        public int GetHeartBeatInterval()
        {
            int heartBtInt = 60;
       
            if (!string.IsNullOrEmpty(this._heartBeatInt))
            {
                if (Int32.TryParse(this._heartBeatInt, out heartBtInt) == true)
                {
                    // TODO: Log errors if heartbeat not converted to int. 
                }
            }
            return heartBtInt;
        }

        /// <summary>
        /// Read FIX properties and sets the respective parameters
        /// </summary>
        private void PopulateFixSettings()
        {
            try
            {
                // Get parameter values
                var settings = ReadFixSettingsFile.GetSettings(_fixSettingsFile);

                // Assign parameter values
                if (settings != null && settings.Count > 0)
                {
                    settings.TryGetValue("SenderCompID", out _quoteSenderCompId);
                    settings.TryGetValue("TargetCompID", out _quoteTargetCompId);
                    settings.TryGetValue("DeliverToCompID", out _deliverToCompId);
                    settings.TryGetValue("SenderSubID", out _senderSubId);
                    settings.TryGetValue("HeartBtInt", out _heartBeatInt);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "PopulateFixSettings");
            }
        }
    }
}

