ALTER TABLE Category
ADD ModerateTopics bit NULL, ModeratePosts bit NULL
GO

ALTER TABLE Post
ADD Pending bit NULL
GO

ALTER TABLE Topic
ADD Pending bit NULL
GO


