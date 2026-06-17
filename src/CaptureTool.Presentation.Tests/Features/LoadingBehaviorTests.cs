using CaptureTool.Presentation.Loading;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class LoadingBehaviorTests
{
    [TestMethod]
    public void Loadable_Load_MarksLoaded()
    {
        var loadable = new TestLoadable();

        loadable.Load();

        Assert.AreEqual(LoadState.Loaded, loadable.LoadState);
        Assert.IsTrue(loadable.IsLoaded);
        Assert.IsFalse(loadable.IsLoading);
    }

    [TestMethod]
    public void LoadableWithParam_LoadObject_ForCorrectType_PassesParameterAndMarksLoaded()
    {
        var loadable = new TestLoadableWithParam<string>();

        loadable.Load("capture");

        Assert.AreEqual("capture", loadable.Parameter);
        Assert.AreEqual(typeof(string), loadable.ParameterType);
        Assert.IsTrue(loadable.IsLoaded);
    }

    [TestMethod]
    public void LoadableWithParam_LoadObject_WithNullReferenceType_PassesNull()
    {
        var loadable = new TestLoadableWithParam<string>();

        loadable.Load((object?)null);

        Assert.IsNull(loadable.Parameter);
        Assert.IsTrue(loadable.IsLoaded);
    }

    [TestMethod]
    public void LoadableWithParam_LoadObject_WithNullValueType_Throws()
    {
        var loadable = new TestLoadableWithParam<int>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => loadable.Load(null));
        StringAssert.Contains(exception.Message, typeof(int).FullName);
    }

    [TestMethod]
    public void LoadableWithParam_LoadObject_WithWrongType_Throws()
    {
        var loadable = new TestLoadableWithParam<int>();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => loadable.Load("not an integer"));
        StringAssert.Contains(exception.Message, typeof(int).FullName);
    }

    [TestMethod]
    public async Task AsyncLoadable_LoadAsync_MarksLoaded()
    {
        var loadable = new TestAsyncLoadable();

        await loadable.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(loadable.IsLoaded);
    }

    [TestMethod]
    public async Task AsyncLoadableWithParam_LoadObject_ForCorrectType_PassesParameterAndMarksLoaded()
    {
        var loadable = new TestAsyncLoadableWithParam<string>();

        await loadable.LoadAsync("capture", TestContext.CancellationToken);

        Assert.AreEqual("capture", loadable.Parameter);
        Assert.AreEqual(typeof(string), loadable.ParameterType);
        Assert.IsTrue(loadable.IsLoaded);
    }

    [TestMethod]
    public async Task AsyncLoadableWithParam_LoadObject_WithNullReferenceType_PassesNull()
    {
        var loadable = new TestAsyncLoadableWithParam<string>();

        await loadable.LoadAsync((object?)null, TestContext.CancellationToken);

        Assert.IsNull(loadable.Parameter);
        Assert.IsTrue(loadable.IsLoaded);
    }

    [TestMethod]
    public async Task AsyncLoadableWithParam_LoadObject_WithNullValueType_Throws()
    {
        var loadable = new TestAsyncLoadableWithParam<int>();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            loadable.LoadAsync(null, TestContext.CancellationToken));
        StringAssert.Contains(exception.Message, typeof(int).FullName);
    }

    [TestMethod]
    public void HasLoadStateBase_ExposesLoadingAndErrorStates()
    {
        var loadable = new TestLoadable();

        loadable.MarkLoading();
        Assert.IsTrue(loadable.IsLoading);

        loadable.MarkError();
        Assert.AreEqual(LoadState.Error, loadable.LoadState);
        Assert.IsFalse(loadable.IsLoading);
        Assert.IsFalse(loadable.IsLoaded);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class TestLoadable : Loadable
    {
        public void MarkLoading() => StartLoading();
        public void MarkError() => LoadingError();
    }

    private sealed class TestLoadableWithParam<T> : LoadableWithParam<T>
    {
        public T? Parameter { get; private set; }

        public override void Load(T parameter)
        {
            Parameter = parameter;
            base.Load(parameter);
        }
    }

    private sealed class TestAsyncLoadable : AsyncLoadable
    {
    }

    private sealed class TestAsyncLoadableWithParam<T> : AsyncLoadableWithParam<T>
    {
        public T? Parameter { get; private set; }

        public override Task LoadAsync(T parameter, CancellationToken cancellationToken)
        {
            Parameter = parameter;
            return base.LoadAsync(parameter, cancellationToken);
        }
    }
}
