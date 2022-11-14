using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Extensions.Logging;
using PTI.Microservices.Library.Configuration;
using PTI.Microservices.Library.Interceptors;
using PTI.Microservices.Library.Models.AzureFaceService.DetectFaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Services
{
    /// <summary>
    /// Service in charge of exposing access to Azure Face
    /// </summary>
    public sealed class AzureFaceService
    {
        private ILogger<AzureFaceService> Logger { get; set; }
        private AzureFaceConfiguration AzureFaceConfiguration { get; set; }
        private CustomHttpClient CustomHttpClient { get; set; }
        private FaceClient FaceClient { get; }

        /// <summary>
        /// Creates a new instance of <see cref="AzureFaceService"/>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="azureFaceConfiguration"></param>
        /// <param name="customHttpClient"></param>
        public AzureFaceService(ILogger<AzureFaceService> logger, AzureFaceConfiguration azureFaceConfiguration,
            CustomHttpClient customHttpClient)
        {
            this.Logger = logger;
            this.AzureFaceConfiguration = azureFaceConfiguration;
            this.CustomHttpClient = customHttpClient;
            Microsoft.Azure.CognitiveServices.Vision.Face.FaceClient faceClient =
                new Microsoft.Azure.CognitiveServices.Vision.Face.FaceClient(
                    new ApiKeyServiceClientCredentials(this.AzureFaceConfiguration.Key), httpClient: customHttpClient,
                    disposeHttpClient: false)
                {
                    Endpoint = this.AzureFaceConfiguration.Endpoint
                };
            this.FaceClient = faceClient;
        }

        private const string RECOGNITIONMODEL = "recognition_03";

        /// <summary>
        /// Gets the person with the specified id in the specified large group
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="personId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Person> GetPersonInLargePersonGroupByPersonId(Guid largePersonGroupId,
            Guid personId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await this.FaceClient.LargePersonGroupPerson.GetAsync(largePersonGroupId.ToString(),
                    personId, cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the person with the specified name in the specified large group
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="personName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Person> GetPersonInLargePersonGroupByPersonName(Guid largePersonGroupId,
            string personName, CancellationToken cancellationToken = default)
        {
            try
            {
                var allPersonsInGroup = await this.FaceClient.LargePersonGroupPerson
                    .ListAsync(largePersonGroupId.ToString(), cancellationToken: cancellationToken);
                var personWithMatchingName = allPersonsInGroup.Where(p => p.Name == personName).SingleOrDefault();
                return personWithMatchingName;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Trains a large person group
        /// </summary>
        /// <param name="largePeersonGroupId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task TrainLargePersonGroupAsync(Guid largePeersonGroupId, CancellationToken cancellationToken = default)
        {
            try
            {
                await this.FaceClient.LargePersonGroup.TrainAsync(largePeersonGroupId.ToString(), cancellationToken);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Identify a person in the specified image url
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="largePersonGroupId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<IdentifyResult>> IdentifySinglePersonAsync(Uri imageUrl,
            Guid largePersonGroupId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                List<IdentifyResult> results = new List<IdentifyResult>();
                IList<FaceAttributeType> faceAttributeTypes = new List<FaceAttributeType>() {
                    FaceAttributeType.Accessories,
                    FaceAttributeType.Age,
                    FaceAttributeType.Blur,
                    FaceAttributeType.Emotion,
                    FaceAttributeType.Exposure,
                    FaceAttributeType.FacialHair,
                    FaceAttributeType.Gender,
                    FaceAttributeType.Glasses,
                    FaceAttributeType.Hair,
                    FaceAttributeType.HeadPose,
                    FaceAttributeType.Makeup,
                    FaceAttributeType.Noise,
                    FaceAttributeType.Occlusion,
                    FaceAttributeType.Smile
                };
                var facesDetectionResult = await this.FaceClient.Face.DetectWithUrlAsync(imageUrl.ToString(),
                    returnFaceId: true, returnFaceAttributes: faceAttributeTypes, recognitionModel: RECOGNITIONMODEL,
                    cancellationToken: cancellationToken);
                var faceIds = facesDetectionResult.Where(p => p.FaceId != null).Select(p => p.FaceId.Value).ToList();
                foreach (var singleFace in facesDetectionResult)
                {
                    var identifyPersonResult = await this.FaceClient.Face.IdentifyAsync(faceIds,
                        largePersonGroupId: largePersonGroupId.ToString(),
                        cancellationToken: cancellationToken);
                    results.AddRange(identifyPersonResult);
                }
                return results;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Get a persisted face information
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<PersistedFace> GetFaceInLargePersonGroup(Guid largePersonGroupId, Guid personId, Guid faceId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await this.FaceClient.LargePersonGroupPerson.GetFaceAsync(largePersonGroupId.ToString(),
                    personId, faceId, cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Adds a face from an url to the specified person in the specified large person group
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="personId"></param>
        /// <param name="imageUrl"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<PersistedFace> AddFaceToPersonInLargePersonGroupAsync(Guid largePersonGroupId, Guid personId,
            Uri imageUrl,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userData = JsonSerializer.Serialize(new
                {
                    SourceUrl = imageUrl.ToString()
                });
                var result =
                await this.FaceClient.LargePersonGroupPerson.AddFaceFromUrlAsync(largePersonGroupId.ToString(), personId,
                    imageUrl.ToString(), userData: userData, cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new person under the specified large person group
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="personName"></param>
        /// <param name="ignoreDuplicateNameRestriction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Person> AddPersonToLargePersonGroupAsync(Guid largePersonGroupId,
            string personName, bool ignoreDuplicateNameRestriction = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var existentPerson = await this.GetPersonInLargePersonGroupByPersonName(largePersonGroupId,
                    personName, cancellationToken);
                if (!ignoreDuplicateNameRestriction)
                {
                    if (existentPerson != null)
                    {
                        throw new Exception("Library is designed to not support more than 1 person with the same name");
                    }
                }
                var result = await this.FaceClient.LargePersonGroupPerson.CreateAsync(largePersonGroupId.ToString(),
                    name: personName, userData: null, cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the large person group with matching name
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<LargePersonGroup> GetLargePersonGroupByGroupId(Guid largePersonGroupId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await this.FaceClient.LargePersonGroup.GetAsync(largePersonGroupId.ToString());
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the large person group with matching name
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<LargePersonGroup> GetLargePersonGroupByName(string groupName, CancellationToken cancellationToken = default)
        {
            try
            {
                var groupsList = await this.FaceClient.LargePersonGroup.ListAsync(cancellationToken: cancellationToken);
                var groupWithMatchingName = groupsList.Where(p => p.Name == groupName).SingleOrDefault();
                return groupWithMatchingName;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all persons in the specified large person group
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<Person>> GetAllPersonsInLargePersonGroupAsync(Guid largePersonGroupId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await this.FaceClient.LargePersonGroupPerson.ListAsync(largePersonGroupId.ToString(),
                    cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<LargePersonGroup>> GetAllLargePersonGroupsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await this.FaceClient.LargePersonGroup.ListAsync(returnRecognitionModel: true,
                    cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Deletes all large person groups with the specified name
        /// </summary>
        /// <param name="largePersonGroupId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeletePersonGroupById(Guid largePersonGroupId, CancellationToken cancellationToken = default)
        {
            try
            {
                await this.FaceClient.LargePersonGroup.DeleteAsync(largePersonGroupId.ToString());
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Deletes all large person groups with the specified name
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteAllLargePersonGroupByName(string groupName, CancellationToken cancellationToken = default)
        {
            try
            {
                var allGroups = await this.FaceClient.LargePersonGroup.ListAsync(cancellationToken: cancellationToken);
                var allGroupsWithMatchingName = allGroups.Where(p => p.Name == groupName);
                if (allGroupsWithMatchingName.Count() > 0)
                {
                    foreach (var singleGroupToDelete in allGroupsWithMatchingName)
                    {
                        await this.FaceClient.LargePersonGroup.DeleteAsync(singleGroupToDelete.LargePersonGroupId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a new person group
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="ignoreDuplicatedNameRestriction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The generated id for the group</returns>
        public async Task<Guid> CreateLargePersonGroupAsync(string groupName,
            bool ignoreDuplicatedNameRestriction = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ignoreDuplicatedNameRestriction)
                {
                    var existentGroup = await this.GetLargePersonGroupByName(groupName, cancellationToken);
                    if (existentGroup != null)
                    {
                        throw new Exception("Library is designed to not support more than 1 group with the same name");
                    }
                }
                Guid newGroupId = Guid.NewGuid();
                //Check https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/599acdee6ac60f11b48b5a9d
                await this.FaceClient.LargePersonGroup.CreateAsync(newGroupId.ToString(), name: groupName,
                    recognitionModel: RECOGNITIONMODEL, cancellationToken: cancellationToken);
                return newGroupId;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Detects the faces in the specified image
        /// </summary>
        /// <param name="imageUri"></param>
        /// <param name="faceAttributes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<DetectedFace>> DetectFacesAsync(Uri imageUri, List<FaceAttributes> faceAttributes = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IList<FaceAttributeType> lstFaceAttributes = new List<FaceAttributeType>()
                {
                    FaceAttributeType.Accessories,
                    FaceAttributeType.Age,
                    FaceAttributeType.Blur,
                    FaceAttributeType.Emotion,
                    FaceAttributeType.Exposure,
                    FaceAttributeType.FacialHair,
                    FaceAttributeType.Gender,
                    FaceAttributeType.Glasses,
                    FaceAttributeType.Hair,
                    FaceAttributeType.HeadPose,
                    FaceAttributeType.Makeup,
                    FaceAttributeType.Noise,
                    FaceAttributeType.Occlusion,
                    FaceAttributeType.Smile
                };
                var response = await FaceClient.Face.DetectWithUrlWithHttpMessagesAsync(imageUri.ToString(),
                    returnFaceAttributes: lstFaceAttributes);
                if (response.Response.IsSuccessStatusCode)
                {
                    return response.Body;
                }
                else
                {
                    string reason = response.Response.ReasonPhrase;
                    string detailedError = await response.Response.Content.ReadAsStringAsync();
                    throw new Exception($"Reason: {reason}. Details: {detailedError}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Detects the faces in the specified image
        /// </summary>
        /// <param name="imageStream"></param>
        /// <param name="faceAttributes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<DetectedFace>> DetectFacesAsync(Stream imageStream, List<FaceAttributes> faceAttributes = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                IList<FaceAttributeType> lstFaceAttributes = new List<FaceAttributeType>()
                {
                    FaceAttributeType.Accessories,
                    FaceAttributeType.Age,
                    FaceAttributeType.Blur,
                    FaceAttributeType.Emotion,
                    FaceAttributeType.Exposure,
                    FaceAttributeType.FacialHair,
                    FaceAttributeType.Gender,
                    FaceAttributeType.Glasses,
                    FaceAttributeType.Hair,
                    FaceAttributeType.HeadPose,
                    FaceAttributeType.Makeup,
                    FaceAttributeType.Noise,
                    FaceAttributeType.Occlusion,
                    FaceAttributeType.Smile
                };
                var response = await FaceClient.Face.DetectWithStreamWithHttpMessagesAsync(imageStream,
                    returnFaceAttributes: lstFaceAttributes);
                if (response.Response.IsSuccessStatusCode)
                {
                    return response.Body;
                }
                else
                {
                    string reason = response.Response.ReasonPhrase;
                    string detailedError = await response.Response.Content.ReadAsStringAsync();
                    throw new Exception($"Reason: {reason}. Details: {detailedError}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
