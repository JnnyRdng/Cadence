using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace Mobile;

internal static class Notifications
{
    public static Notification BuildPersistent(Context context, string channelId, string title)
    {
        EnsureChannel(context, channelId);

        // Intent that opens the app when the notification is tapped.
        var openIntent = new Intent(context, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPending = PendingIntent.GetActivity(
            context, 0, openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        // Stop action button on the notification itself.
        var stopIntent = new Intent(context, typeof(CadenceService));
        stopIntent.SetAction(CadenceService.ActionStop);
        var stopPending = PendingIntent.GetService(
            context, 0, stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        return new NotificationCompat.Builder(context, channelId)
            .SetContentTitle(title)?
            .SetContentText("Tap to open. Pull down to stop.")?
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)?
            .SetContentIntent(openPending)?
            .AddAction(global::Android.Resource.Drawable.IcMediaPause, "Stop", stopPending)?
            .SetOngoing(true)?  // user can't swipe it away
            .SetPriority(NotificationCompat.PriorityLow)?
            .Build() ?? throw new NullReferenceException(nameof(NotificationCompat.PriorityLow));
    }

    private static void EnsureChannel(Context context, string channelId)
    {
        // Notification channels are required from Android 8 (Oreo). Idempotent.
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;

        var existing = manager.GetNotificationChannel(channelId);
        if (existing is not null) return;

        var channel = new NotificationChannel(channelId, "Cadence sensor", NotificationImportance.Low)
        {
            Description = "Persistent notification while cadence is being broadcast.",
        };
        manager.CreateNotificationChannel(channel);
    }
}