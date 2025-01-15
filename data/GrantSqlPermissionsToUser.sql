param @UserName nvarchar(100)

create user @UserName from external provider;
alter role db_datareader add member @UserName;
alter role db_datawriter add member @UserName;
