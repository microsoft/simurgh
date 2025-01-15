-- Description: This script drops all tables and then creates them again.
-- todo: remove this and create a script that references individual scripts to create tables
if exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestionAnswer'
) begin
    drop table SurveyQuestionAnswer
end

if exists (
    select
        *
    from
        sys.fulltext_indexes
    where 
        object_id = object_id('dbo.SurveyQuestionAnswer')
) begin
    drop fulltext index on SurveyQuestionAnswer
end

if exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyResponse'
) begin
    drop table SurveyResponse
end

if exists (
    select
        *
    from
        sys.fulltext_indexes
    where 
        object_id = object_id('dbo.SurveyQuestion')
) begin
    drop fulltext index on SurveyQuestion
end

if exists (
    select
        *
    from
        sys.tables
where
    name = 'SurveyQuestion'
) begin
    drop table SurveyQuestion
end

if exists (
    select
        *
    from
        sys.tables
    where
        name = 'Survey'
) begin
    drop table Survey
end

if exists (
    select 
        1 
    from 
        sys.fulltext_catalogs 
    where 
        name = 'ftCatalog')
begin
    drop fulltext catalog ftCatalog;
    print 'Full-text catalog deleted.';
end else print 'Full-text catalog does not exist.';

-----------------------------------------------------------------
print('All tables dropped successfully');
-----------------------------------------------------------------

if not exists (
    select 
        1 
    from 
        sys.fulltext_catalogs
    where 
        name = 'ftcatalog')
begin
    create fulltext catalog ftcatalog as default;
    print 'Full-text catalog created.';
end else print 'Full-text catalog already exists.';

if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'Survey'
) begin
    create table Survey
    (
        Id uniqueidentifier primary key,
        Filename varchar(max),
        Version varchar(50)
    );
    print 'Table Survey created successfully';
end else print 'Table Survey already exists';

if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyResponse'
) begin
    create table SurveyResponse
    (
        Id uniqueidentifier primary key,
        SurveyId uniqueidentifier,
        constraint FK_Survey_SurveyResponse foreign key (SurveyId) references Survey (Id)
    );
    print 'Table SurveyResponse created successfully';
end else print 'Table SurveyResponse already exists';

if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestion'
) begin
    create table SurveyQuestion
    (
        Id uniqueidentifier,
        SurveyId uniqueidentifier,
        Question nvarchar (max),
        DataType varchar(20) null,
        Description nvarchar (max) null,
        Embedding vector(1536) null,
        constraint PK_SurveyQuestion_Id primary key clustered (Id),
        constraint FK_Survey_SurveyQuestion foreign key (SurveyId) references Survey (Id)
    );
    print 'Table SurveyQuestion created successfully';
end else print 'Table SurveyQuestion already exists';

if not exists (
    select
        *
    from
        sys.fulltext_indexes
    where 
        object_id = object_id('dbo.SurveyQuestion')
) begin
    create fulltext index on SurveyQuestion
    (
        Question language 1033,
        Description language 1033
    )
    key index PK_SurveyQuestion_Id
    with stoplist = system;
    print 'Full text index created for SurveyQuestion';
end else print 'Full text index for SurveyQuestion already exists.';

if not exists (
    select
        *
    from
        sys.tables
    where
        name = 'SurveyQuestionAnswer'
) begin
    -- create table
    create table SurveyQuestionAnswer
    (
        Id uniqueidentifier,
        SurveyId uniqueidentifier,
        SurveyResponseId uniqueidentifier,
        SurveyQuestionId uniqueidentifier,
        TextAnswer nvarchar (max) null,
        NumericAnswer numeric null,
        SentimentAnalysisJson nvarchar (max) null,
        PositiveSentimentConfidenceScore float(53) null,
        NeutralSentimentConfidenceScore float(53) null,
        NegativeSentimentConfidenceScore float(53) null,
        Embedding vector(1536) null,
        constraint PK_SurveyQuestionAnswer_Id primary key clustered (Id),
        constraint FK_Survey_SurveyQuestionAnswer foreign key (SurveyId) references Survey (Id),
        constraint FK_SurveyResponse_SurveyQuestionAnswer foreign key (SurveyResponseId) references SurveyResponse (Id),
        constraint FK_SurveyQuestion_SurveyQuestionAnswer foreign key (SurveyQuestionId) references SurveyQuestion (Id)
    );
    print 'Table SurveyQuestionAnswer created successfully';
end else print 'Table SurveyQuestionAnswer already exists';

if not exists (
    select
        *
    from
        sys.fulltext_indexes
    where 
        object_id = object_id('dbo.SurveyQuestionAnswer')
) begin
    create fulltext index on SurveyQuestionAnswer
    (
        TextAnswer language 1033
    )
    key index PK_SurveyQuestionAnswer_Id
    with stoplist = system;
    print 'Full text index created for SurveyQuestionAnswer';
end else print 'Full text index for SurveyQuestionAnswer already exists.';

print('All tables created successfully');
