using System;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace SensorCoreWeb
{
    public class SensorCoreHub : Hub
    {
        public void Send(string walkingSteps, string runningSteps, string activity)
        {
            Clients.All.broadcastMessage(walkingSteps, runningSteps, activity);
        }
    }
}