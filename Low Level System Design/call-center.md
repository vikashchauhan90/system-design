# Call Center

There are three categories of workers at a call centre: supervisors, managers, and attendants. By default, the attendant will answer the call. If the attendant is unavailable or unable to assist the customer, the call will be forwarded to the appropriate management; if the manager is unable to assist the customer, the call will be forwarded to their supervisor.

```C#
public abstract class Employee
{
    public string Name { get; set; }
    public bool IsAvailable { get; set; }
    public Employee? Supervisor { get; set; }

    protected Employee(string name)
    {
        Name = name;
        IsAvailable = true;
    }

    public virtual bool HandleCall(Call call)
    {

        if (IsAvailable)
        {
            IsAvailable = false;
            var callHandled = CallHandled(call);
            IsAvailable = true;
            if (!callHandled)
            {
                return EscalateCall(call);
            }
            return callHandled;
        }
        return false;
    }

    private bool CallHandled(Call call) => call != null;
    private bool EscalateCall(Call call)
    {
        if (Supervisor != null)
        {
            return Supervisor.HandleCall(call);
        }
        return false;
    }
}

public class Attendant : Employee
{
    public Attendant(string name) : base(name) { }
}

public class Manager : Employee
{
    public Manager(string name) : base(name) { }
}

public class Supervisor : Employee
{
    public Supervisor(string name) : base(name) { }
}

public class Call
{
    public string CustomerName { get; set; }
    public string Query { get; set; }

    public Call(string customerName, string query)
    {
        CustomerName = customerName;
        Query = query;
    }
}

public class CallCenter
{
    private List<Employee> employees;

    public CallCenter()
    {
        employees = new List<Employee>();
    }

    public void AddEmployee(Employee employee)
    {
        employees.Add(employee);
    }

    public void RouteCall(Call call)
    {
        foreach (var employee in employees)
        {
            // check if employee available
            if (!employee.IsAvailable)
            {
                continue;
            }

            // check if call handled by the employee
            if (employee.HandleCall(call))
            {
                return;
            }
        }
    }
}
```