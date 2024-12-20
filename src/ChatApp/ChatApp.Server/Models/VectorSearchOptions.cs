namespace ChatApp.Server.Models;

// todo: better negotiate what the contract is between the SK function and the repository/service class for SQL
public class VectorSearchOptions
{
    // general constructor required for serialization
    public VectorSearchOptions() { }
    public VectorSearchOptions(Guid surveyId, int topK, List<OrderByClause>? orderByClauses = null, List<WhereClause>? whereClauses = null)
    {
        SurveyId = surveyId;
        TopK = topK;
        OrderByClauses = orderByClauses;
        WhereClauses = whereClauses;
    }

    // these need to be able to have default values for serialization
    public Guid? SurveyId { get; set; } = null;
    public int? TopK { get; set; } = null;
    public List<OrderByClause>? OrderByClauses { get; set; } = null;
    public List<WhereClause>? WhereClauses { get; set; } = null;

    public string GetOrderByClauseString()
    {
        return "";
    }
    public string GetWhereClauseString()
    {
        return "";
    }
}

// todo: build out nested where clause capabilities
// todo: build out enum for operators
// todo: build out enum for column names
// todo: convert to classes with logic on ToString => would this impact serialization process for SK?
public record WhereClause
{
    // general constructor required for serialization
    public WhereClause() { }
    public WhereClause(string columnName, string operatorStr, string? value = null, List<WhereClause>? whereClauses = null)
    {
        ColumnName = columnName;
        Operator = operatorStr;
        Value = value;
        WhereClauses = whereClauses;
    }
    public override string ToString()
    {
        return "";
    }
    // these need to be able to have default values for serialization
    public string? ColumnName { get; set; } = null;
    public string? Operator { get; set; } = null;
    public string? Value { get; set; } = null;
    public List<WhereClause>? WhereClauses = null;
    public string ToSqlString()
    {
        return "";
    }
}

public class OrderByClause
{
    // general constructor required for serialization
    public OrderByClause() { }
    public OrderByClause(string columnName, bool descending)
    {
        ColumnName = columnName;
        Descending = descending;
    }
    // these need to be able to have default values for serialization
    public string? ColumnName { get; set; } = null;
    public bool? Descending { get; set; } = null;
    public string ToSqlString()
    {
        return "";
    }
}
