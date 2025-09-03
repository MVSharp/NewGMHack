using MessagePack;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder()
                 .ConfigureServices(services =>
                  {
                      // services.AddSingleton<MainWindow>();
                      // services.AddSingleton<MainViewModel>();
                      services.AddHostedService<TestServices>();
                      services.AddMessagePipe()
                              .AddNamedPipeInterprocess("test",
                                                        options =>
                                                        {
                                                            options.InstanceLifetime   = InstanceLifetime.Singleton;
                                                            //options.HostAsServer = false;
                                                            
                                                            options.MessagePackSerializerOptions =
                                                                MessagePackSerializerOptions.Standard;
                                                        });

                      services.AddSingleton<MyService>();
                  }).Build();
await host.StartAsync();
var t = host.Services.GetRequiredService<MyService>();
 await t.ReceiveAsync();
 while (true)
 {
     await Task.Delay(1);
 }
public class MyService
{
    //private readonly IDistributedPublisher<string, int> _publisher;
    private readonly IDistributedSubscriber<string, int> _subscriber;

    // public MyService(IDistributedPublisher<string, int> publisher,
    public MyService(
                     IDistributedSubscriber<string, int> subscriber)
    {
        // _publisher = publisher;
        _subscriber = subscriber;
    }

    // public async Task SendAsync()
    // {
    //     await _publisher.PublishAsync("my-key", 42);
    // }

    public async Task ReceiveAsync()
    {
        await _subscriber.SubscribeAsync("my-key", x =>
        {
            Console.WriteLine($"Received: {x}");
        });
    }
}
// example: server handler
public class MyAsyncHandler : IAsyncRequestHandler<int, string>
{
    public async ValueTask<string> InvokeAsync(int request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1);
        if (request == -1)
        {
            throw new Exception("NO -1");
        }
        else
        {
            return "ECHO:" + request.ToString();
        }
    }
}

public class MyAsyncHandler2: IAsyncRequestHandler<int, string>
{
    public async ValueTask<string> InvokeAsync(int request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1);
        if (request == -1)
        {
            throw new Exception("NO -1");
        }
        else
        {
            return "ECHO2:" + request.ToString();
        }
    }
}
public class TestServices(IRemoteRequestHandler<int, string> remoteHandler) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(4000);
        // await A();
    }

// client
    async Task A()
    {
        var v = await remoteHandler.InvokeAsync(9999);
        Console.WriteLine(v); // ECHO:9999
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}