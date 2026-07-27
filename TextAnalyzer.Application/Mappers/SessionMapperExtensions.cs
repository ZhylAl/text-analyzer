using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;

namespace TextAnalyzer.Application.Mappers
{
    public static class SessionMapperExtensions
    {
        public static SessionEntity ToEntity(this SessionSaveDto dto)
        {
            var entity = new SessionEntity
            {
                Id = Guid.NewGuid(),
                StartedAt = dto.StartedAt,
                FinishedAt = dto.FinishedAt,
                ExecutionModeId = dto.ExecutionModeId,
                Files = new List<FileEntity>(),
                Results = new List<ResultEntity>()
            };

            foreach (var resultDto in dto.Results)
            {
                var fileEntity = new FileEntity
                {
                    FilePath = resultDto.FilePath,
                    FileHash = resultDto.Hash 
                };

                var resultEntity = new ResultEntity
                {
                    File = fileEntity,
                    CharCount = resultDto.AnalysisResult.CharCount,
                    WordCount = resultDto.AnalysisResult.WordCount,
                    LineCount = resultDto.AnalysisResult.LineCount,
                    LongestWord = resultDto.AnalysisResult.LongestWord
                };

                entity.Files.Add(fileEntity);
                entity.Results.Add(resultEntity);
            }

            return entity;
        }
    }
}
