using Microsoft.AspNetCore.SignalR;

namespace Firey.API
{
    public class BroadcastService : BackgroundService
    {
        private readonly KilnControlService controller;
        private readonly IHubContext<ControlHub, IControlHub> hub;

        public BroadcastService(KilnControlService controller, IHubContext<ControlHub, IControlHub> hub)
        {
            this.controller = controller;
            this.hub = hub;
        }

        private KilnInfo currentInfo;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.controller.OnUpdate += i => this.currentInfo = i;
            this.controller.OnScheduleChange += s => this.hub.Clients.All.CurrentSchedule(s);
            
            while(!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
                await this.hub.Clients.All.Update(this.currentInfo);
            }
        }
    }

    public interface IControlHub
    {
        Task Update(KilnInfo info);

        Task CurrentSchedule(KilnSchedule? schedule);
    }

    public class ControlHub : Hub<IControlHub>
    {
        private readonly KilnControlService kiln;

        public ControlHub(KilnControlService kiln)
        {
            this.kiln = kiln;
        }

        public override Task OnConnectedAsync()
        {
            this.Clients.All.CurrentSchedule(kiln.Schedule);

            return base.OnConnectedAsync();
        }
    }
}
