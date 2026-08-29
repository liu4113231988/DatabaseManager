using DatabaseInterpreter.Utility;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 核心库 <see cref="FeedbackInfo"/> → <c>Action&lt;string&gt;</c> 的公共反馈桥接。
/// 替代各服务/VM 中重复的私有 FeedbackObserver 实现。
/// </summary>
public sealed class FeedbackBridge : IObserver<FeedbackInfo>
{
    private readonly Action<string>? _onFeedback;

    public FeedbackBridge(Action<string>? onFeedback)
    {
        _onFeedback = onFeedback;
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
        => _onFeedback?.Invoke($"错误：{error.Message}");

    public void OnNext(FeedbackInfo value)
    {
        if (!string.IsNullOrWhiteSpace(value.Message))
        {
            _onFeedback?.Invoke(value.Message);
        }
    }
}
