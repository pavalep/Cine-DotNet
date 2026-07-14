namespace Simba.Core.Interfaces;
public interface IConfigService
{
    string? Get(string key, string? defaultValue = null);
    void Set(string key, string value);
}