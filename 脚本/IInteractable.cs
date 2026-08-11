// 交互对象统一接口：任何可交互实体都实现该协议。
public interface IInteractable
{
    // 交互提示文案
    string InteractionPrompt { get; }

    // 是否可交互
    bool IsInteractable { get; }

    // 触发交互
    void Interact(PlayerController player);
}
