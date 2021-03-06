﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConnectSdk.Api;
using ConnectSdk.Config;
using ConnectSdk.Store;

namespace ConnectSdk
{
    public class ConnectClient : IConnect
    {
        protected readonly IEventStore EventStore;
        protected readonly IEventEndpoint HttpEventEndpoint;

        public ConnectClient(IConfiguration configuration, IEventStore eventStore = null)
        {
            EventStore = eventStore ?? new FileEventStore();
            HttpEventEndpoint = new HttpEventEndpoint(configuration);
        }

        public ConnectClient(IEventEndpoint eventEndpoint, IEventStore eventStore = null)
        {
            EventStore = eventStore ?? new FileEventStore();
            HttpEventEndpoint = eventEndpoint;
        }

        public virtual async Task<EventPushResponse> Push(string collectionName, object eventData)
        {
            return await HttpEventEndpoint.Push(collectionName, new Event(eventData));
        }

        public virtual async Task<EventBatchPushResponse> Push(string collectionName, IEnumerable<object> eventData)
        {
            return await HttpEventEndpoint.Push(collectionName, eventData.Select(ed => new Event(ed)));
        }

        public virtual async Task Add(string collectionName, object eventData)
        {
            await EventStore.Add(collectionName, new Event(eventData));
        }

        public virtual async Task Add(string collectionName, IEnumerable<object> eventData)
        {
            await TaskEx.WhenAll(eventData.Select(ed => EventStore.Add(collectionName, new Event(ed))));
        }

        public virtual async Task<EventBatchPushResponse> PushPending()
        {
            var events = await EventStore.ReadAll();
            var results = await HttpEventEndpoint.Push(events);

            foreach (var keyedResponses in results.ResponsesByCollection)
            {
                foreach (var eventPushResponse in keyedResponses.Value)
                {
                    var status = eventPushResponse.Status;
                    if (status == EventPushResponseStatus.Successfull || status == EventPushResponseStatus.Duplicate)
                        await EventStore.Acknowlege(keyedResponses.Key, eventPushResponse.Event);
                }
            }

            return results;
        }
    }
}
