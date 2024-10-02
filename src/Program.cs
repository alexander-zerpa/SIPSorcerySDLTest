using System.Net;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.SDL2;
using SIPSorceryMedia.Abstractions;

using System.CommandLine;

class Program {
    static ManualResetEvent exitMre = new ManualResetEvent(false);

    static string audioDeviceDefault;

    static SIPUserAgent userAgent;
    static SIPTransport sipTransport;

    static MediaEndPoints audioEndPoints;

    static async Task<int> Main(string[] args) {
        var portOption = new Option<int>(
                name: "--port",
                description: "port to use for SIP communications.",
                getDefaultValue: () => 5060
                );

        var audioDeviceOption = new Option<string>(
                name: "--device",
                description: "string to search for on device setup.",
                getDefaultValue: () => ""
                );

        var destinationArg = new Argument<string>(
                name: "destintation",
                description: "uri to call"
                );

        var rootCommand = new RootCommand("SIP Client test app");
        rootCommand.AddOption(portOption);
        rootCommand.AddGlobalOption(audioDeviceOption);

        rootCommand.SetHandler((sipPort, audioDevice) => {
                    audioDeviceDefault = audioDevice;
                    SetUp(sipPort);
                    setExit();
                }, portOption, audioDeviceOption);

        var callCommand = new Command("call", "makes a call.") {
            destinationArg
        };
        rootCommand.AddCommand(callCommand);

        callCommand.SetHandler((destination, audioDevice, sipPort) => {
                    audioDeviceDefault = audioDevice;
                    SetUp(sipPort);
                    MakeCall(destination);
                    setExit();
                }, destinationArg, audioDeviceOption, portOption);

        return await rootCommand.InvokeAsync(args);
    }

    static void SetUp(int sipPort) {
        sipTransport = new SIPTransport();
        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, sipPort)));

        Console.WriteLine($"Running on port: {sipPort}");

        audioEndPoints = AudioSetUp();

        userAgent = new SIPUserAgent(sipTransport, null, false);

        userAgent.OnIncomingCall += async (ua, req) => {
            Console.WriteLine($"Incoming call from {req.RemoteSIPEndPoint}.");

            var uas = ua.AcceptCall(req);

            if (userAgent.IsCallActive) {
                uas.Reject(SIPResponseStatusCodesEnum.BusyHere, null);
                Console.WriteLine("Rejected: already on call.");
            } else {
                var voipMediaSession = new VoIPMediaSession(audioEndPoints);
                voipMediaSession.AcceptRtpFromAny = true;
                await ua.Answer(uas, voipMediaSession);
                Console.WriteLine("Answered.");
            }
            // await audioSource.PauseAudio();
            // await voipMediaSession.AudioExtrasSource.StartAudio();
            // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);
        };

        userAgent.OnCallHungup += (dialog) => { Console.WriteLine($"{dialog.RemoteSIPEndPoint} hanged up"); };
        userAgent.RemotePutOnHold += () => { Console.WriteLine("Put on hold."); };
        userAgent.RemoteTookOffHold += () => { Console.WriteLine("Took off hold."); };
    }

    static void setExit() {
        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
            e.Cancel = true;

            if (userAgent.IsCallActive) {
                Console.WriteLine("Hanging up.");
                userAgent.Hangup();
            } else if (userAgent.IsCalling || userAgent.IsRinging) {
                Console.WriteLine("Cancelling call.");
                userAgent.Cancel();
            }
            exitMre.Set();
        };

        exitMre.WaitOne();

        while (userAgent.IsHangingUp) {}

        Console.WriteLine("Shutting down.");

        sipTransport.Shutdown();
    }

    static async Task<bool> MakeCall(string destination) {
        var voipMediaSession = new VoIPMediaSession(audioEndPoints);
        voipMediaSession.AcceptRtpFromAny = true;

        Console.WriteLine("making call.");
        var callResult = await userAgent.Call(destination, null, null, voipMediaSession);
        Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");

        // var audioSource = voipMediaSession.Media.AudioSource;
        // await audioSource.PauseAudio();
        // await voipMediaSession.AudioExtrasSource.StartAudio();
        // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);

        return callResult;
    }

    static MediaEndPoints AudioSetUp() { return AudioSetUp(audioDeviceDefault); }
    static MediaEndPoints AudioSetUp(string deviceQuerry) { return AudioSetUp(deviceQuerry, deviceQuerry); }
    static MediaEndPoints AudioSetUp(string inDeviceQuerry, string outDeviceQuerry) {
        SDL2Helper.InitSDL();

        string outDevice = SDL2Helper.GetAudioPlaybackDevice(inDeviceQuerry);
        string inDevice = SDL2Helper.GetAudioRecordingDevice(outDeviceQuerry);

        Console.WriteLine($"Using input device: {inDevice}");
        Console.WriteLine($"Using output device: {outDevice}");

        var audioEncoder = new AudioEncoder();

        var audioSink = new SDL2AudioEndPoint(outDevice, audioEncoder);
        var audioSource = new SDL2AudioSource(inDevice, audioEncoder);

        audioSink.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);
        audioSource.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // audioSink.SetAudioSinkFormat(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
        // await audioSink.StartAudioSink();

        return new MediaEndPoints{
            AudioSource = audioSource,
            AudioSink = audioSink,
        };
    }
}
