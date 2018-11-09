﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Elastic.Agent.Core.DiagnosticSource;
using Elastic.Agent.Core.Model.Payload;

namespace Elastic.Agent.Core.DiagnosticListeners
{
    public class HttpDiagnosticListener : IDiagnosticListener
    {
        public string Name => "HttpHandlerDiagnosticListener";

        //TODO: find better way to keep track of respones
        private readonly ConcurrentDictionary<HttpRequestMessage, DateTime> _startedRequests = new ConcurrentDictionary<HttpRequestMessage, DateTime>();

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(KeyValuePair<string, object> kv)
        {
            switch (kv.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start": //TODO: look for consts 
                    var val = kv.Value.GetType().GetTypeInfo().GetDeclaredProperty("Request").GetValue(kv.Value) as HttpRequestMessage;
                    if (val != null)
                    {
                        var added = _startedRequests.TryAdd(val, DateTime.UtcNow);
                    }
                    break;

                case "System.Net.Http.HttpRequestOut.Stop":
                    var request = kv.Value.GetType().GetTypeInfo().GetDeclaredProperty("Request").GetValue(kv.Value) as HttpRequestMessage;
                    var response = kv.Value.GetType().GetTypeInfo().GetDeclaredProperty("Response").GetValue(kv.Value) as HttpResponseMessage;
                    var requestTaskStatus = (TaskStatus)kv.Value.GetType().GetTypeInfo().GetDeclaredProperty("RequestTaskStatus").GetValue(kv.Value);

                    var transactionStartTime = TransactionContainer.Transactions.Value[0].TimestampInDateTime;
                    var utcNow = DateTime.UtcNow;

                    var span = new Span
                    {
                        Start = (decimal)(utcNow - transactionStartTime).TotalMilliseconds,
                        Name =  $"{request.Method} {request.RequestUri.ToString()}",
                        Type =  "Http",
                        Context = new Span.ContextC
                        {
                            Http = new Http
                            {
                                Url = request.RequestUri.ToString() //TODO: don't we repost response code, and other things? Intake
                            }
                        }
                    };
              
                    if (_startedRequests.TryRemove(request, out DateTime requestStart))
                    {
                        var requestDuration = DateTime.UtcNow - requestStart; //TODO: there are better ways
                        span.Duration = requestDuration.TotalMilliseconds;
                    }

                    TransactionContainer.Transactions.Value[0].Spans.Add(span);
                    break;
                default:
                    break;
            }
        }
    }
}
