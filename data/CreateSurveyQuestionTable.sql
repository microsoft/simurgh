if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestion'
) begin
create table
    SurveyQuestion (
        Id uniqueidentifier primary key,
        SurveyId uniqueidentifier,
        Question nvarchar (max),
        DataType varchar(20) null,
        Description nvarchar (max) null,
        constraint FK_Survey foreign key (SurveyId) references Survey (Id)
    );

print 'Table SurveyQuestion created successfully';

end else print 'Table SurveyQuestion already exists';