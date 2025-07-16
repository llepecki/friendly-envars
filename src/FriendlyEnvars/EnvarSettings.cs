using System.Globalization;

namespace FriendlyEnvars;

public record EnvarSettings
{
    internal EnvarSettings()
    {
        EnvarPropertyBinder = new DefaultEnvarPropertyBinder();
        Culture = CultureInfo.InvariantCulture;
        IsOptionsSnapshotAllowed = false;
        IsOptionsMonitorAllowed = false;
    }

    public EnvarSettings UseCustomEnvarPropertyBinder(IEnvarPropertyBinder binder)
    {
        EnvarPropertyBinder = binder;
        return this;
    }

    public EnvarSettings UseCulture(CultureInfo culture)
    {
        Culture = culture;
        return this;
    }

    public EnvarSettings AllowOptionsSnapshot()
    {
        IsOptionsSnapshotAllowed = true;
        return this;
    }

    public EnvarSettings AllowOptionsMonitor()
    {
        IsOptionsMonitorAllowed = true;
        return this;
    }

    internal IEnvarPropertyBinder EnvarPropertyBinder { get; private set; }

    internal CultureInfo Culture { get; private set; }

    internal bool IsOptionsSnapshotAllowed { get; private set; }

    internal bool IsOptionsMonitorAllowed { get; private set; }
}
