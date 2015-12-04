﻿/*
Project Orleans Cloud Service SDK ver. 1.0
 
Copyright (c) Microsoft Corporation
 
All rights reserved.
 
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
associated documentation files (the ""Software""), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Orleans.MultiCluster;
using System;
using System.Collections.Generic;

namespace Orleans.Runtime.MultiClusterNetwork
{
    class MultiClusterOracleData : IMultiClusterGossipData
    {
        private volatile MultiClusterData localdata;  // immutable, can read without lock

        private readonly HashSet<GrainReference> confListeners;

        private readonly TraceLogger logger;

        internal MultiClusterData Current { get { return localdata; } }

        internal MultiClusterOracleData(TraceLogger log)
        {
            logger = log;
            localdata = new MultiClusterData();
            confListeners = new HashSet<GrainReference>();
        }

        internal bool SubscribeToMultiClusterConfigurationEvents(GrainReference observer)
        {
            if (logger.IsVerbose2)
                logger.Verbose2("SubscribeToMultiClusterConfigurationEvents: {0}", observer);

            if (confListeners.Contains(observer))
                return false;

            confListeners.Add(observer);
            return true;
        }

        internal bool UnSubscribeFromMultiClusterConfigurationEvents(GrainReference observer)
        {
            if (logger.IsVerbose3)
                logger.Verbose3("UnSubscribeFromMultiClusterConfigurationEvents: {0}", observer);

            return confListeners.Remove(observer);
        }


        public MultiClusterData ApplyDataAndNotify(MultiClusterData data)
        {
            if (data.IsEmpty)
                return data;

            MultiClusterData delta;
            MultiClusterData prev = localdata;

            localdata = prev.Merge(data, out delta);

            if (logger.IsVerbose2)
                logger.Verbose2("ApplyDataAndNotify: delta {0}", delta);

            if (delta.IsEmpty)
                return delta;

            if (delta.Configuration != null)
            {
                // notify configuration listeners of change
                foreach (var listener in confListeners)
                {
                    try
                    {
                        if (logger.IsVerbose2)
                            logger.Verbose2("-NotificationWork: notify IProtocolParticipant {0} of configuration {1}", listener, delta.Configuration);

                        // enqueue conf change event as grain call
                        var g = InsideRuntimeClient.Current.InternalGrainFactory.Cast<IProtocolParticipant>(listener);
                        g.OnMultiClusterConfigurationChange(delta.Configuration).Ignore();
                    }
                    catch (Exception exc)
                    {
                        logger.Error(ErrorCode.MultiClusterNetwork_LocalSubscriberException,
                            String.Format("IProtocolParticipant {0} threw exception processing configuration {1}",
                            listener, delta.Configuration), exc);
                    }
                }
            }

            return delta;
        }
    }
}
