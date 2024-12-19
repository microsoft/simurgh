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
        SurveyQuestionId uniqueidentifier,
        TextAnswer nvarchar (max) null,
        NumericAnswer numeric null,
        SentimentAnalysisJson nvarchar (max) null,
        PositiveSentimentConfidenceScore numeric null,
        NeutralSentimentConfidenceScore numeric null,
        NegativeSentimentConfidenceScore numeric null,
        constraint FK_Survey_SurveyQuestionAnswer foreign key (SurveyId) references Survey (Id),
        constraint FK_SurveyResponse_SurveyQuestionAnswer foreign key (SurveyResponseId) references SurveyResponse (Id),
        constraint FK_SurveyQuestion_SurveyQuestionAnswer foreign key (SurveyQuestionId) references SurveyQuestion (Id)
    );

print 'Table SurveyQuestionAnswer created successfully';

end else print 'Table SurveyQuestionAnswer already exists';