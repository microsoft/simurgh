if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyResponse'
) begin
create table
    SurveyResponse (
        Id uniqueidentifier primary key,
        SurveyId uniqueidentifier
    );

print 'Table SurveyResponse created successfully';

end else print 'Table SurveyResponse already exists';