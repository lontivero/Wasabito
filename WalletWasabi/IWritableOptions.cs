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

public interface IWritableOptions<out TOptions> : IOptionsSnapshot<TOptions>, IOptionsMonitor<TOptions> where TOptions : class, new()
{ 
	void Update(Action<TOptions> applyChanges, bool reload = false);
}

public class WritableOptions<TOptions> : IWritableOptions<TOptions> where TOptions : class, new()
 {
     private readonly IOptionsMonitor<TOptions> _options;
     private readonly IConfiguration _configuration;
     private readonly string _section;
     private readonly string _file;

     public WritableOptions(
         IOptionsMonitor<TOptions> options,
         IConfiguration configuration,
         string section,
         string file)
     {
         _options = options;
         _configuration = configuration;
         _section = section;
         _file = file;
     }

     public TOptions CurrentValue => _options.CurrentValue;
     public TOptions Value => _options.CurrentValue;
     public TOptions Get(string name) => _options.Get(name);
     public IDisposable OnChange(Action<TOptions, string> listener) => _options.OnChange(listener);

     public void Update(Action<TOptions> applyChanges, bool reload = false)
     {
	     var fileContent = File.Exists(_file)
		     ? File.ReadAllText(_file)
			 : "{}";
         var jObject = JsonConvert.DeserializeObject<JObject>(fileContent, JsonSerializationOptions.Default.Settings);
         var sectionObject = jObject.TryGetValue(_section, out var section) 
	         ? JsonConvert.DeserializeObject<TOptions>(section.ToString(), JsonSerializationOptions.Default.Settings) 
	         : Value;

         applyChanges(sectionObject);

         jObject[_section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
         File.WriteAllText(_file, JsonConvert.SerializeObject(jObject, Formatting.Indented));
     }
 }