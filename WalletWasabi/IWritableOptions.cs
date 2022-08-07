using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi;

public interface IWritableOptions<out T> : IOptions<T> where T : class, new()
{ 
	void Update(Action<T> applyChanges);
}

public class WritableOptions<T> : IWritableOptions<T> where T : class, new()
 {
     private readonly IConfigurationBuilder _environment;
     private readonly IOptionsMonitor<T> _options;
     private readonly IConfigurationRoot _configuration;
     private readonly string _section;
     private readonly string _file;

     public WritableOptions(
         IOptionsMonitor<T> options,
         IConfigurationRoot configuration,
         string section,
         string file)
     {
         _options = options;
         _configuration = configuration;
         _section = section;
         _file = file;
     }

     public T Value => _options.CurrentValue;
     public T Get(string name) => _options.Get(name);

     public void Update(Action<T> applyChanges)
     {
         var fileProvider = _configuration.Providers.OfType<JsonConfigurationProvider>();
         var fileInfo = fileProvider.First().Source.FileProvider.GetFileInfo(_file);
         var physicalPath = fileInfo.PhysicalPath;

         var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
         var sectionObject = jObject.TryGetValue(_section, out JToken section) 
	         ? JsonConvert.DeserializeObject<T>(section.ToString(), JsonSerializationOptions.Default.Settings) 
	         : (Value ?? new T());

         applyChanges(sectionObject);

         jObject[_section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
         File.WriteAllText(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));
         _configuration.Reload();
     }
 }