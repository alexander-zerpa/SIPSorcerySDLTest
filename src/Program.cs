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
                    IncomingCall();
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
        SDL2Helper.InitSDL();

        sipTransport = new SIPTransport();
        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, sipPort)));

        userAgent = new SIPUserAgent(sipTransport, null);

        Console.WriteLine($"Running on port: {sipPort}");
    }

    static void IncomingCall() {
        userAgent.OnIncomingCall += async (ua, req) => {
            Console.WriteLine($"Incoming call from {req.RemoteSIPEndPoint}.");

            var voipMediaSession = SetUpVoIPMediaSession();

            var uas = ua.AcceptCall(req);

            await ua.Answer(uas, voipMediaSession);

            // await audioSource.PauseAudio();
            // await voipMediaSession.AudioExtrasSource.StartAudio();
            // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);
        };
    }

    static void setExit() {
        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
            e.Cancel = true;

            if (userAgent.IsCallActive) {
                Console.WriteLine("Hanging up.");
                userAgent.Hangup();
            } else {
                Console.WriteLine("Cancelling call.");
                userAgent.Cancel();
            }
            exitMre.Set();
        };

        exitMre.WaitOne();

        sipTransport.Shutdown();
    }

    static async Task<bool> MakeCall(string destination) {
        var voipMediaSession = SetUpVoIPMediaSession();

        Console.WriteLine("making call.");
        var callResult = await userAgent.Call(destination, null, null, voipMediaSession);
        Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");

        // var audioSource = voipMediaSession.Media.AudioSource;
        // await audioSource.PauseAudio();
        // await voipMediaSession.AudioExtrasSource.StartAudio();
        // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);

        return callResult;
    }

    static VoIPMediaSession SetUpVoIPMediaSession() { return SetUpVoIPMediaSession(audioDeviceDefault); }
    static VoIPMediaSession SetUpVoIPMediaSession(string deviceQuerry) { return SetUpVoIPMediaSession(deviceQuerry, deviceQuerry); }
    static VoIPMediaSession SetUpVoIPMediaSession(string inDeviceQuerry, string outDeviceQuerry) {
        string outDevice = SDL2Helper.GetAudioPlaybackDevice(inDeviceQuerry);
        string inDevice = SDL2Helper.GetAudioRecordingDevice(outDeviceQuerry);

        Console.WriteLine(outDevice);
        Console.WriteLine(inDevice);

        var audioEncoder = new AudioEncoder();

        var audioSink = new SDL2AudioEndPoint(outDevice, audioEncoder);
        var audioSource = new SDL2AudioSource(inDevice, audioEncoder);

        audioSink.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);
        audioSource.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // audioSink.SetAudioSinkFormat(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
        // await audioSink.StartAudioSink();

        var mediaEndPoints = new MediaEndPoints{
            AudioSource = audioSource,
            AudioSink = audioSink,
        };

        var voipMediaSession = new VoIPMediaSession(mediaEndPoints);
        voipMediaSession.AcceptRtpFromAny = true;

        return voipMediaSession;
    }
}
