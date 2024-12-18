if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestionAnswer'
) begin
-- create table
create table
    SurveyQuestionAnswer (
        Id uniqueidentifier primary key,
        SurveyId uniqueidentifier,
        SurveyResponseId uniqueidentifier,
        QuestionId uniqueidentifier,
        TextAnswer nvarchar (max) null,
        NumericAnswer numeric null,
        SentimentAnalysisJson nvarchar (max) null,
        constraint FK_SurveyResponse_Survey foreign key (SurveyId) references Survey (Id),
        constraint FK_SurveyResponse_Question foreign key (QuestionId) references SurveyQuestion (Id)
    );

print 'Table SurveyQuestionAnswer created successfully';

end else print 'Table SurveyQuestionAnswer already exists';