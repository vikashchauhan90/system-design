# Shopping Site

```C#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Product
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int Quantity { get; set; }

    public Product(string name, decimal price, int quantity)
    {
        Name = name;
        Price = price;
        Quantity = quantity;
    }
}

public class Store
{
    private List<Product> _products = new List<Product>();

    public Store(List<Product> products)
    {
        _products = products;
    }

    public List<Product> GetProducts()
    {
        return _products;
    }
}

public class Cart
{
    private Dictionary<Product, DateTime> _products = new Dictionary<Product, DateTime>();

    public void AddProduct(Product product)
    {
        if (product.Quantity <= 0)
        {
            Console.WriteLine($"Cannot add product {product.Name}. It is not available.");
            return;
        }

        _products[product] = DateTime.Now.AddMinutes(15);
        product.Quantity--;
    }

    public decimal GetTotalPrice()
    {
        decimal total = 0;
        foreach (var product in _products.Keys)
        {
            total += product.Price;
        }
        return total;
    }

    public void CheckReservations()
    {
        var expiredReservations = _products.Where(p => p.Value < DateTime.Now).ToList();
        foreach (var reservation in expiredReservations)
        {
            reservation.Key.Quantity++;
            _products.Remove(reservation.Key);
        }
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var products = new List<Product>
        {
            new Product("Apple", 0.5m, 10),
            new Product("Banana", 0.3m, 20),
            new Product("Cherry", 0.2m, 30)
        };

        var store = new Store(products);
        var cart = new Cart();

        var apple = store.GetProducts().Find(p => p.Name == "Apple");
        cart.AddProduct(apple);

        Console.WriteLine($"Total price: {cart.GetTotalPrice()}");

        // Simulate waiting for 16 minutes
        await Task.Delay(TimeSpan.FromMinutes(16));

        cart.CheckReservations();

        Console.WriteLine($"Total price after checking reservations: {cart.GetTotalPrice()}");
    }
}

```