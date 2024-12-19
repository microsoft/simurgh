if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestionAnswerVector'
) begin
-- create table
create table
    SurveyQuestionAnswerVector (
        Id int primary key identity (1, 1),
        SurveyQuestionAnswerId uniqueidentifier not null,
        Vector float not null,
        constraint FK_SurveyQuestionAnswer_SurveyQuestionAnswerVector foreign key (SurveyQuestionAnswerId) references SurveyQuestionAnswer (Id)
    );

print 'Table SurveyQuestionAnswerVector created successfully';

end else print 'Table SurveyQuestionAnswerVector already exists';