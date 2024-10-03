using System.Net;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.SDL2;
using SIPSorceryMedia.Abstractions;

class Program {
    static void Main(string[] args) {
        int sipPort = 5060;

        // CancellationTokenSource exitCts = new CancellationTokenSource();
        ManualResetEvent exitMre = new ManualResetEvent(false);

        var sipTransport = new SIPTransport();
        sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, sipPort)));

        Console.WriteLine($"Running on port: {sipPort}");

        SDL2Helper.InitSDL();

        string deviceQuerry = "plan";

        string outDevice = SDL2Helper.GetAudioPlaybackDevice(deviceQuerry);
        string inDevice = SDL2Helper.GetAudioRecordingDevice(deviceQuerry);

        Console.WriteLine($"Using input device: {inDevice}");
        Console.WriteLine($"Using output device: {outDevice}");

        var audioEncoder = new AudioEncoder();

        var audioSink = new SDL2AudioEndPoint(outDevice, audioEncoder);
        var audioSource = new SDL2AudioSource(inDevice, audioEncoder);

        audioSink.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);
        audioSource.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // audioSink.SetAudioSinkFormat(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
        // await audioSink.StartAudioSink();

        // var audioEndPoints = new MediaEndPoints{
        //     AudioSource = audioSource,
        //     AudioSink = audioSink,
        // };

        VoIPMediaSession voipMediaSession;

        var userAgent = new SIPUserAgent(sipTransport, null);

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
            // exitCts.Cancel();
        };

        userAgent.OnIncomingCall += async (ua, req) => {
            Console.WriteLine($"Incoming call from {req.RemoteSIPEndPoint}.");

            var uas = ua.AcceptCall(req);

            if (userAgent.IsCallActive) {
                uas.Reject(SIPResponseStatusCodesEnum.BusyHere, null);
                Console.WriteLine("Rejected: already on call.");
            } else {
                voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                            AudioSource = audioSource,
                            AudioSink = audioSink,
                        });
                voipMediaSession.AcceptRtpFromAny = true;

                await ua.Answer(uas, voipMediaSession);
                Console.WriteLine("Answered.");
            }
            // await audioSource.PauseAudio();
            // await voipMediaSession.AudioExtrasSource.StartAudio();
            // voipMediaSession.AudioExtrasSource.SetSource(AudioSourcesEnum.Music);
        };

            
        userAgent.ClientCallTrying += async (uac, res) => { Console.WriteLine("Trying..."); };
        userAgent.ClientCallRinging += async (uac, res) => { Console.WriteLine("Ringing..."); };
        userAgent.ClientCallFailed += async (uac, e, res) => { Console.WriteLine($"Failed: {e}."); };
        userAgent.ClientCallAnswered += async (uac, res) => { Console.WriteLine("Call successful."); };

        userAgent.OnCallHungup += async (dialog) => { Console.WriteLine("Hanging up."); };

        userAgent.RemotePutOnHold += async () => { 
            Console.WriteLine("Put on hold."); 
            voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                    AudioSource = audioSource,
                    AudioSink = audioSink,
                    });
            voipMediaSession.AcceptRtpFromAny = true;
        };
        userAgent.RemoteTookOffHold += async () => { 
            Console.WriteLine("Took off hold."); 
            voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                    AudioSource = audioSource,
                    AudioSink = audioSink,
                    });
            voipMediaSession.AcceptRtpFromAny = true;
        };

        Task.Run(async () => {
                while (!exitMre.WaitOne(0)) {
                // while (!exitCts.Token.WaitHandle.WaitOne(0)) {
                    var keyProps = Console.ReadKey();
                    Console.Write('\b');

                    switch (keyProps.KeyChar) {
                        case 'h':
                            if (userAgent.IsCallActive) {
                                if (userAgent.IsOnLocalHold) {
                                    // await (userAgent.MediaSession as VoIPMediaSession).AudioExtrasSource.PauseAudio();
                                    // // (userAgent.MediaSession as VoIPMediaSession).TakeOffHold();
                                    // await (userAgent.MediaSession as VoIPMediaSession).Media.AudioSource.ResumeAudio();
                                    // // await (userAgent.MediaSession as VoIPMediaSession).Media.AudioSink.ResumeAudioSink();
                                    // await (userAgent.MediaSession as VoIPMediaSession).Media.AudioSink.StartAudioSink();

                                    userAgent.TakeOffHold();

                                    voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                                            AudioSource = audioSource,
                                            AudioSink = audioSink,
                                            });
                                    voipMediaSession.AcceptRtpFromAny = true;

                                    Console.WriteLine("Taking remote off hold.");
                                } else {
                                    // // await (userAgent.MediaSession as VoIPMediaSession).PutOnHold();
                                    // await (userAgent.MediaSession as VoIPMediaSession).Media.AudioSource.PauseAudio();
                                    // await (userAgent.MediaSession as VoIPMediaSession).Media.AudioSink.PauseAudioSink();
                                    // await (userAgent.MediaSession as VoIPMediaSession).AudioExtrasSource.StartAudio();
                                    // (userAgent.MediaSession as VoIPMediaSession).AudioExtrasSource.SetSource(AudioSourcesEnum.Music);

                                    userAgent.PutOnHold();
                                    
                                    voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                                            AudioSource = audioSource,
                                            AudioSink = audioSink,
                                            });
                                    voipMediaSession.AcceptRtpFromAny = true;
                                    
                                    Console.WriteLine("Putting remote on hold.");
                                }
                            } else {
                                Console.WriteLine("No active call.");
                            }
                            break;
                        case 'm':
                            if (!userAgent.IsCallActive) {
                                Console.WriteLine("destination: ");
                                var destination = Console.ReadLine();

                                voipMediaSession = new VoIPMediaSession(new MediaEndPoints{
                                        AudioSource = audioSource,
                                        AudioSink = audioSink,
                                        });
                                voipMediaSession.AcceptRtpFromAny = true;

                                Console.WriteLine($"Calling {destination}");
                                userAgent.Call(destination, null, null, voipMediaSession);
                            } else {
                                Console.WriteLine("Already on call.");
                            }
                            break;
                        case 'q':
                            exitMre.Set();
                            // exitCts.Cancel();
                            goto case 'c';
                        case 'c':
                            if (userAgent.IsCallActive) {
                                userAgent.Hangup();
                            } else if (userAgent.IsCalling || userAgent.IsRinging) {
                                userAgent.Cancel();
                            } else {
                                Console.WriteLine("No on call.");
                            }
                            break;
                        default:
                            Console.WriteLine("invalid key");
                            break;
                    }
                }
        });

        exitMre.WaitOne();
        // exitCts.Token.WaitHandle.WaitOne();

        while (userAgent.IsHangingUp) {}

        Console.WriteLine("Shutting down.");

        sipTransport.Shutdown();
    }
}
