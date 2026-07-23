using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace JobFlow.Infrastructure.Persistence;

public static class MongoDbConfig
{
    public static void Configure()
    {
        // This is a global setting for the BSON library
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }
}
