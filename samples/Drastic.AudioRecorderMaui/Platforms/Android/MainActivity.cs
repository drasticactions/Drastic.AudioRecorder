using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace Drastic.AudioRecorderMaui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        if (ContextCompat.CheckSelfPermission (this, Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            ActivityCompat.RequestPermissions (this, new String []{ Manifest.Permission.RecordAudio }, 1);
        }
    }
}
