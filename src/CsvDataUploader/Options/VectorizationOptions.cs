using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvDataUploader.Options;

public class VectorizationOptions
{
    // include if using managed identity in a multi-tenant environment
    public string TenantId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    // leave off if using managed identity
    public string? Key { get; set; }
    public string Deployment { get; set; } = string.Empty;
}
