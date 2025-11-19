using FluentValidation;
using RemoteFileManager.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Application.Validators
{
    public class UploadFileValidator : AbstractValidator<UploadRequestDto>
    {
        public UploadFileValidator()
        {
            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("File name is required")
                .Must(BeAValidFileName).WithMessage("Invalid file name");

            RuleFor(x => x.DestinationPath)
                .NotEmpty().WithMessage("Destination path is required");

            RuleFor(x => x.TotalSize)
                .GreaterThan(0).WithMessage("File size must be greater than 0")
                .LessThanOrEqualTo(524288000).WithMessage("File size must not exceed 500MB");
        }

        private bool BeAValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }
    }
}
