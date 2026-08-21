namespace TruthInTheFlip.Farm.Format;

public class SelectorTemplate<TStats, TProduct>
{
    //answer the question does this stat fit in this product
    public Func<TProduct, TStats, bool> Selector { get; init; }

    //answer the question do we use this product
    public Func<TProduct, bool> Use { get; init; }

    public SelectorTemplate(Func<TProduct, TStats, bool> selector)
    {
        this.Selector = selector;
        this.Use = _ => true;
    }

    protected SelectorTemplate(SelectorTemplate<TStats, TProduct> source, Func<TProduct, bool> use)
    {
        this.Selector = source.Selector;
        this.Use = (q) => source.Use(q) && use(q);
    }
}