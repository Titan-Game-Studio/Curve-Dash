// Namespace and interface for Gem Socket Service
namespace STG.CurveDash
{
    public interface IGemSocketService
    {
        // Trả về kết quả; không throw exception
        GemSocketResult TrySocketGem(GemItemData gem, IEquippedLoadout loadout);
    }
}
