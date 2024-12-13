create table SurveyQuestionAnswer (
    Id uniqueidentifier primary key,
    SurveyId uniqueidentifier,
    SurveyResponseId uniqueidentifier,
    QuestionId uniqueidentifier,
    TextAnswer nvarchar(max) null,
    NumericAnswer numeric null,
    constraint FK_SurveyResponse_Survey foreign key (SurveyId) references Survey(Id),
    constraint FK_SurveyResponse_Question foreign key (QuestionId) references SurveyQuestion(Id)
);
