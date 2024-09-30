using System.Net;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.FFmpeg;
using SIPSorceryMedia.SDL2;
using SIPSorceryMedia.Abstractions;

class Program {
    static async Task Main(string[] args) {
        var sipTransport = new SIPTransport();

        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, int.Parse(args[0]))));

        var userAgent = new SIPUserAgent(sipTransport, null, true);
        
        var audioSource = new FFmpegAudioSource(new AudioEncoder());
        audioSource.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // string ffmpegBinPath = Environment.GetEnvironmentVariable("FFMPEG_BIN");

        // FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL, "/run/current-system/sw/bin");
        // FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL);

        SDL2Helper.InitSDL();

        string deviceName = SDL2Helper.GetAudioPlaybackDevice("Plan");

        var audioSink = new SDL2AudioEndPoint(deviceName, new AudioEncoder());
        // audioSink.SetAudioSinkFormat(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
        // await audioSink.StartAudioSink();

        var mediaEndPoints = new MediaEndPoints{
            AudioSource = audioSource,
            AudioSink = audioSink,
        };

        var voipMediaSession = new VoIPMediaSession(mediaEndPoints);
        voipMediaSession.AcceptRtpFromAny = true;

        Console.WriteLine("making call.");
        var callResult = await userAgent.Call(args[1], null, null, voipMediaSession);
        Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");

        // await audioSource.PauseAudio();
        // await voipMediaSession.AudioExtrasSource.StartAudio();
        // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);
        

        Console.ReadLine();
    }
}
