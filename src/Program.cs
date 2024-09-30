using System.Net;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.FFmpeg;
using SIPSorceryMedia.SDL2;
using SIPSorceryMedia.Abstractions;

class Program {
    static ManualResetEvent exitMre = new ManualResetEvent(false);

    static void Main(string[] args) {
        var sipTransport = new SIPTransport();

        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, int.Parse(args[0]))));

        var userAgent = new SIPUserAgent(sipTransport, null);

        // string mic = SDL2Helper.GetAudioRecordingDevice("Plan");
        // Console.WriteLine(mic);

        var audioSource = new FFmpegAudioSource(new AudioEncoder());
        // var audioSource = new FFmpegMicrophoneSource($"audio={mic}", new AudioEncoder());
        audioSource.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // string ffmpegBinPath = Environment.GetEnvironmentVariable("FFMPEG_BIN");

        // FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL, "/run/current-system/sw/bin");
        // FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL);

        SDL2Helper.InitSDL();

        string deviceName = SDL2Helper.GetAudioPlaybackDevice("Plan");
        Console.WriteLine(deviceName);

        var audioSink = new SDL2AudioEndPoint(deviceName, new AudioEncoder());
        // audioSink.SetAudioSinkFormat(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
        // await audioSink.StartAudioSink();

        var mediaEndPoints = new MediaEndPoints{
            AudioSource = audioSource,
            AudioSink = audioSink,
        };

        var voipMediaSession = new VoIPMediaSession(mediaEndPoints);
        voipMediaSession.AcceptRtpFromAny = true;


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

        userAgent.OnIncomingCall += async (ua, req) => {
            Console.WriteLine($"Incoming call from {req.RemoteSIPEndPoint}.");

            var uas = ua.AcceptCall(req);

            await ua.Answer(uas, voipMediaSession);

            await audioSource.PauseAudio();
            await voipMediaSession.AudioExtrasSource.StartAudio();
            voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);
        };

        // MakeCall(userAgent, args[1], voipMediaSession);

        exitMre.WaitOne();

        sipTransport.Shutdown();

    }

    static async Task<bool> MakeCall(SIPUserAgent ua, string destination, VoIPMediaSession voipMediaSession) {
        Console.WriteLine("making call.");
        var callResult = await ua.Call(destination, null, null, voipMediaSession);
        Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");

        var audioSource = voipMediaSession.Media.AudioSource;

        await audioSource.PauseAudio();
        await voipMediaSession.AudioExtrasSource.StartAudio();
        voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);

        return callResult;
    }
}
