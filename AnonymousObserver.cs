namespace MuteMic;

// AudioSwitcher exposes bare IObservable<T> with no System.Reactive dependency;
// this is the minimal adapter to subscribe with a plain Action<T>.
internal sealed class AnonymousObserver<T>(Action<T> onNext) : IObserver<T>
{
    public void OnNext(T value) => onNext(value);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
