using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FortniteCinematicSettings.Models;

public sealed class QualitySetting : INotifyPropertyChanged
{
    private int _value;

    public QualitySetting(string key, string name, string description, string section, int value)
    {
        Key = key;
        Name = name;
        Description = description;
        Section = section;
        _value = value;
    }

    public string Key { get; }
    public string Name { get; }
    public string Description { get; }
    public string Section { get; }

    public int Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
