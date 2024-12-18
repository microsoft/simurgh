if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'Survey'
) begin
create table
    Survey (
        Id uniqueidentifier primary key,
        Filename varchar(max),
        Version varchar(50)
    );

print 'Table Survey created successfully';

end else print 'Table Survey already exists';