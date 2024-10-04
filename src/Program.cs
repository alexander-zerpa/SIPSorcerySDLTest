using System.Net;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.SDL2;
using SIPSorceryMedia.Abstractions;

class Program
{
    static int SIP_PORT = 5060;

    static SIPTransport? Transport;
    static SIPUserAgent? UserAgent;

    static SDL2AudioEndPoint? Sink;
    static SDL2AudioSource? Source;

    static VoIPMediaSession? AudioSession;

    // static CancellationTokenSource exitCts = new CancellationTokenSource();
    static ManualResetEvent exitMre = new ManualResetEvent(false);


    static void Main(string[] args)
    {
        SetUp();
        SetHandlers();

        Task.Run(RunUserIO);

        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            Hangup();
            exitMre.Set();
            // exitCts.Cancel();
        };

        exitMre.WaitOne();
        // exitCts.Token.WaitHandle.WaitOne();

        while (UserAgent.IsHangingUp) { }

        Console.WriteLine("Shutting down.");

        Transport.Shutdown();
    }

    static void SetUp()
    {
        Transport = new SIPTransport();
        Transport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, SIP_PORT)));

        Console.WriteLine($"Running on port: {SIP_PORT}");

        UserAgent = new SIPUserAgent(Transport, null, true);

        SDL2Helper.InitSDL();

        string inDevice = ChooseDevice(false);
        string outDevice = ChooseDevice(true);

        Source = new SDL2AudioSource(inDevice, new AudioEncoder());
        Sink = new SDL2AudioEndPoint(outDevice, new AudioEncoder());

        Sink.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);
        Source.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        Console.WriteLine($"Using input device: {inDevice}");
        Console.WriteLine($"Using output device: {outDevice}");
    }

    static void SetHandlers()
    {
        UserAgent.OnIncomingCall += async (ua, req) =>
        {
            Console.WriteLine($"Incoming call from {req.RemoteSIPEndPoint}.");

            var uas = ua.AcceptCall(req);

            if (UserAgent.IsCallActive)
            {
                uas.Reject(SIPResponseStatusCodesEnum.BusyHere, null);
                Console.WriteLine("Rejected: already on call.");
            }
            else
            {
                AudioSessionSetUp();
                await ua.Answer(uas, AudioSession);
                Console.WriteLine("Answered.");
            }
        };

        UserAgent.ClientCallTrying += (uac, res) => { Console.WriteLine("Trying..."); };
        UserAgent.ClientCallRinging += (uac, res) => { Console.WriteLine("Ringing..."); };
        UserAgent.ClientCallFailed += (uac, e, res) => { Console.WriteLine($"Failed: {e}."); };
        UserAgent.ClientCallAnswered += (uac, res) => { Console.WriteLine("Call successful."); };
        UserAgent.OnCallHungup += (dialog) => { Console.WriteLine("Hanging up."); };

        UserAgent.RemotePutOnHold += () =>
        {
            AudioSessionSetUp();
            Console.WriteLine("Put on hold.");
        };
        UserAgent.RemoteTookOffHold += () =>
        {
            AudioSessionSetUp();
            Console.WriteLine("Took off hold.");
        };
    }

    static void AudioSessionSetUp()
    {
        AudioSession = new VoIPMediaSession(new MediaEndPoints
        {
            AudioSource = Source,
            AudioSink = Sink,
        });
        AudioSession.AcceptRtpFromAny = true;
    }

    static string? ChooseDevice(bool output)
    {
        // TODO: implement device selection
        string querry = "plan";

        return (output) ?
            SDL2Helper.GetAudioPlaybackDevice(querry) :
            SDL2Helper.GetAudioRecordingDevice(querry);
    }

    static void RunUserIO()
    {
        // while (!exitCts.Token.WaitHandle.WaitOne(0))
        while (!exitMre.WaitOne(0))
        {
            var keyProps = Console.ReadKey();
            Console.Write('\b');

            switch (keyProps.KeyChar)
            {
                case 'h':
                    ToggleOnHold();
                    break;
                case 'm':
                    if (!UserAgent.IsCallActive)
                    {
                        MakeCall();
                    }
                    else
                    {
                        Console.WriteLine("Already on call.");
                    }
                    break;
                case 'c':
                    if (!Hangup())
                    {
                        Console.WriteLine("No on call.");
                    }
                    break;
                case 'q':
                    exitMre.Set();
                    // exitCts.Cancel();
                    goto case 'c';
                default:
                    Console.WriteLine("invalid key");
                    break;
            }
        }
    }

    static Task<bool> MakeCall()
    {
        Console.Write("destination: ");
        var destination = Console.ReadLine();

        AudioSessionSetUp();

        Console.WriteLine($"Calling {destination}");
        return UserAgent.Call(destination, null, null, AudioSession);
    }

    static void ToggleOnHold()
    {
        if (UserAgent.IsCallActive)
        {
            if (UserAgent.IsOnLocalHold)
            {
                UserAgent.TakeOffHold();
                AudioSessionSetUp();
                Console.WriteLine("Taking remote off hold.");
            }
            else
            {
                UserAgent.PutOnHold();
                AudioSessionSetUp();
                Console.WriteLine("Putting remote on hold.");
            }
        }
        else
        {
            Console.WriteLine("No active call.");
        }
    }

    static bool Hangup()
    {
        if (UserAgent.IsCallActive)
        {
            UserAgent.Hangup();
        }
        else if (UserAgent.IsCalling || UserAgent.IsRinging)
        {
            UserAgent.Cancel();
        }
        else
        {
            return false;
        }
        return true;
    }
}
