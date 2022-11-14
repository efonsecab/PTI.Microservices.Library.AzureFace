# PTI.Microservices.Library.AzureFace

This is part of PTI.Microservices.Library set of packages

The purpose of this package is to facilitate the calls to Azure Face APIs, while maintaining a consistent usage pattern among the different services in the group

**Examples:**

## Detect Face
    CustomHttpClient customHttpClient = new CustomHttpClient(new CustomHttpClientHandler(null));
    AzureFaceService azureFaceService = new AzureFaceService(null, this.AzureFaceConfiguration, customHttpClient);
    var result = await azureFaceService.DetectFacesAsync(new Uri(this.TestFaceImageUrl));