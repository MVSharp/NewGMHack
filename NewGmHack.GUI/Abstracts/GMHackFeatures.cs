using CommunityToolkit.Mvvm.ComponentModel;
using NewGMHack.Stub;

namespace NewGmHack.GUI.Abstracts
{
    public partial class GMHackFeatures : ObservableObject
    {
        public FeatureName Name { get; set; }

        private readonly Func<GMHackFeatures, Task> _onChanged;

        public  GMHackFeatures(FeatureName name, bool isEnabled, Func<GMHackFeatures, Task> onChanged)
        {
            Name       = name;
            _isEnabled = isEnabled;
            _onChanged = onChanged;
        }

        [ObservableProperty] private bool _isEnabled;

        partial void OnIsEnabledChanged(bool value)
        {
            // Fire-and-forget with error handling
            _ = HandleChangeAsync(this);
        }

        private async Task HandleChangeAsync(GMHackFeatures feature)
        {
            try
            {
                if (_onChanged != null)
                    await _onChanged(feature);
            }
            catch (Exception ex)
            {
                // Log or notify error
            }
        }
    }
}